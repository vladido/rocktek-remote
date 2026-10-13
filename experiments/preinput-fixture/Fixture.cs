// Fixture only: never opens hidserv, never sends input, no remote memory APIs.
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public unsafe class Fixture {
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate uint Send(uint count, IntPtr input, int cb);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate ulong Clock();
 [DllImport("kernel32.dll")] static extern IntPtr VirtualAlloc(IntPtr p, UIntPtr n, uint a, uint prot);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool VirtualProtect(IntPtr p, UIntPtr n, uint prot, out uint old);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool FlushInstructionCache(IntPtr h, IntPtr p, UIntPtr n);
 [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
 [DllImport("kernel32.dll")] public static extern ulong GetTickCount64();
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
 [DllImport("kernel32.dll", CharSet=CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr h, string name);
 [DllImport("ntdll.dll")] static extern bool RtlAddFunctionTable(IntPtr table, uint count, ulong baseAddress);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetProcessMitigationPolicy(IntPtr h, int kind, out ulong flags, int size);
 [DllImport("kernel32.dll", EntryPoint="GetProcessMitigationPolicy", SetLastError=true)] static extern bool GetProcessMitigationPolicyDword(IntPtr h, int kind, out uint flags, int size);
 [DllImport("ntdll.dll")] static extern IntPtr RtlLookupFunctionEntry(ulong pc, out ulong baseAddress, IntPtr history);
 [DllImport("ntdll.dll")] static extern IntPtr RtlVirtualUnwind(uint type, ulong baseAddress, ulong pc, IntPtr entry, IntPtr context, out IntPtr handlerData, out ulong frame, IntPtr pointers);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool VirtualQuery(IntPtr p, out MBI info, UIntPtr n);
 [StructLayout(LayoutKind.Sequential)] struct MBI {public IntPtr Base,AllocationBase; public uint AllocationProtect; public ushort Partition; public UIntPtr RegionSize; public uint State,Protect,Type;}
 static Send original = Mock; static Clock clock = FakeClock;
 static IntPtr originalPtr = Marshal.GetFunctionPointerForDelegate(original);
 static long calls, abiErrors; static ulong fakeNow;
 static bool blockClock; static ManualResetEvent entered=new ManualResetEvent(false), release=new ManualResetEvent(false);
 [ThreadStatic] static uint ec; [ThreadStatic] static IntPtr ep; [ThreadStatic] static int eb;
 static uint Mock(uint n, IntPtr p, int cb) { if(n!=ec || p!=ep || cb!=eb) Interlocked.Increment(ref abiErrors); Interlocked.Increment(ref calls); return 77; }
 static ulong FakeClock() { if(blockClock) {entered.Set(); if(!release.WaitOne(4000)) throw new Exception("clock barrier timeout");} return fakeNow; }
 static void Check(bool v,string what) {if(!v) throw new Exception("FAIL "+what);}
 static int checks;
 static void Assert(bool v,string what) {Check(v,what); checks++;}
 static void Write64(IntPtr p,int off,ulong v) {Marshal.WriteInt64(p,off,unchecked((long)v));}
 class Stub {
  public IntPtr Code;
  public Stub(ulong start,ulong ttl,bool realClock) {
   Check(ttl>0 && ttl<=60000,"bounded ttl"); Check(start<=ulong.MaxValue-ttl,"deadline overflow");
   byte[] code=File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"stub.bin"));
   Check(code.Length<2048,"one page code budget");
   Code=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,4); Check(Code!=IntPtr.Zero,"local allocation");
   Marshal.Copy(code,0,Code,code.Length);
   foreach(string line in File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"offsets.txt"))) {
    string[] x=line.Split(' '); int off=Convert.ToInt32(x[1],16); ulong value=0;
    if(x[0]=="original") value=unchecked((ulong)originalPtr.ToInt64());
    else if(x[0]=="clock") value=unchecked((ulong)(realClock?GetProcAddress(GetModuleHandle("kernel32.dll"),"GetTickCount64"):Marshal.GetFunctionPointerForDelegate(clock)).ToInt64());
    else if(x[0]=="start") value=start; else if(x[0]=="deadline") value=start+ttl; else throw new Exception("symbol");
    Write64(Code,off,value);
   }
   // RUNTIME_FUNCTION + UNWIND_INFO: ENDBR64 (4), sub rsp,72 (4).
   Marshal.WriteInt32(Code,2048,0); Marshal.WriteInt32(Code,2052,code.Length); Marshal.WriteInt32(Code,2056,2060);
   byte[] unwind={1,8,1,0,8,0x82,0,0}; Marshal.Copy(unwind,0,IntPtr.Add(Code,2060),8);
   uint old; Check(VirtualProtect(Code,(UIntPtr)4096,0x20,out old),"RW to RX"); Check(old==4,"initial RW");
   Check(FlushInstructionCache(GetCurrentProcess(),Code,(UIntPtr)4096),"cache flush");
   Check(RtlAddFunctionTable(IntPtr.Add(Code,2048),1,unchecked((ulong)Code.ToInt64())),"unwind register");
   MBI info; Check(VirtualQuery(Code,out info,(UIntPtr)Marshal.SizeOf(typeof(MBI))),"query RX"); Check(info.Protect==0x20,"RX not RWX");
   // Intentionally never free code or unregister metadata: calls may have cached pointer.
  }
 }
 class Slot:IDisposable {
  public MemoryMappedFile map; public MemoryMappedViewAccessor view; byte* ptr;
  public IntPtr Address {get {return (IntPtr)ptr;}}
  public Slot(string name,bool create) {
   map=create?MemoryMappedFile.CreateNew(name,4096):MemoryMappedFile.OpenExisting(name);
   view=map.CreateViewAccessor(0,4096,MemoryMappedFileAccess.ReadWrite);
   byte* raw=null; view.SafeMemoryMappedViewHandle.AcquirePointer(ref raw); ptr=raw+view.PointerOffset;
   Check(((long)ptr&7)==0,"slot aligned");
  }
  public long Read(){return Volatile.Read(ref *(long*)ptr);} // Aligned x64 load; CAS is a write even if unchanged and faults on PAGE_READONLY.
  public long CAS(long value,long expected){return Interlocked.CompareExchange(ref *(long*)ptr,value,expected);}
  public void Initialize(){Check(CAS(originalPtr.ToInt64(),0)==0,"initialize slot");}
  public bool SwapProtected(long value,long expected) {
   uint old; Check(VirtualProtect(Address,(UIntPtr)4096,4,out old),"slot unprotect");
   bool result;
   try {result=CAS(value,expected)==expected;}
   finally {uint ignored; Check(VirtualProtect(Address,(UIntPtr)4096,old,out ignored),"slot protection restore");}
   Check(FlushInstructionCache(GetCurrentProcess(),Address,(UIntPtr)8),"slot cache flush"); return result;
  }
  public void ReadOnly(){uint old; Check(VirtualProtect(Address,(UIntPtr)4096,2,out old),"slot readonly");}
  public void Dispose(){view.SafeMemoryMappedViewHandle.ReleasePointer(); view.Dispose(); map.Dispose();}
 }
 static uint Invoke(IntPtr fn,uint n,IntPtr p,int cb) {
  ec=n;ep=p;eb=cb; return ((Send)Marshal.GetDelegateForFunctionPointer(fn,typeof(Send)))(n,p,cb);
 }
 static IntPtr Input(uint flags) {
  IntPtr p=Marshal.AllocHGlobal(40); for(int i=0;i<40;i++) Marshal.WriteByte(p,i,0);
  Marshal.WriteInt32(p,0,1); Marshal.WriteInt16(p,8,0xA6); Marshal.WriteInt32(p,12,(int)flags); return p;
 }
 static void Unwind(IntPtr code) {
  IntPtr context=Marshal.AllocHGlobal(1232), stack=Marshal.AllocHGlobal(256);
  try {
   foreach(int off in new int[]{8,0x2d,0x44,0x7c}) {
    for(int i=0;i<1232;i++)Marshal.WriteByte(context,i,0);
    ulong sp=unchecked((ulong)stack.ToInt64()), ret=0x123456789ABCUL;
    Marshal.WriteInt32(context,48,0x100003); Write64(context,152,sp);Write64(context,248,unchecked((ulong)code.ToInt64())+(ulong)off);Write64(stack,72,ret);
    ulong image;IntPtr entry=RtlLookupFunctionEntry(unchecked((ulong)code.ToInt64())+(ulong)off,out image,IntPtr.Zero);
    Assert(entry==IntPtr.Add(code,2048)&&image==unchecked((ulong)code.ToInt64()),"lookup dynamic unwind");
    IntPtr handler;ulong frame;RtlVirtualUnwind(0,image,image+(ulong)off,entry,context,out handler,out frame,IntPtr.Zero);
    Assert(unchecked((ulong)Marshal.ReadInt64(context,152))==sp+80,"unwind RSP");
    Assert(unchecked((ulong)Marshal.ReadInt64(context,248))==ret,"unwind RIP");
   }
  }finally{Marshal.FreeHGlobal(context);Marshal.FreeHGlobal(stack);}
  Console.WriteLine("UNWIND native lookup + virtual unwind at 4 body PCs PASS");
 }
 static void Policy() {
  fakeNow=1000; Stub s=new Stub(1000,60000,false);Unwind(s.Code);
  for(int mask=0;mask<2048;mask++) {
   IntPtr p=Input((mask&32)==0?1u:3u); uint n=1; int cb=40;
   if((mask&1)!=0)n=2; if((mask&2)!=0)cb=39; if((mask&4)!=0)Marshal.WriteInt32(p,0,0);
   if((mask&8)!=0)Marshal.WriteInt16(p,8,0xA7); if((mask&16)!=0)Marshal.WriteInt16(p,10,1);
   if((mask&64)!=0)Marshal.WriteInt32(p,12,5); if((mask&128)!=0)Marshal.WriteInt32(p,16,1);
   if((mask&256)!=0)Marshal.WriteInt64(p,24,1);
   // Padding is not an INPUT field and must not change field-based policy.
   if((mask&512)!=0)Marshal.WriteByte(p,4,0x55); if((mask&1024)!=0)Marshal.WriteByte(p,20,0x55);
   bool match=(mask&479)==0; long before=Interlocked.Read(ref calls);
   uint r=Invoke(s.Code,n,p,cb);
   Assert(r==(match?1u:77u),"structural mask "+mask);
   Assert(Interlocked.Read(ref calls)-before==(match?0:1),"forward count "+mask);
   Marshal.FreeHGlobal(p);
  }
  Assert(Invoke(s.Code,1,IntPtr.Zero,40)==77,"null passes untouched");
  Assert(Invoke(s.Code,0,new IntPtr(1),40)==77,"count zero does not dereference");
  Assert(Invoke(s.Code,1,new IntPtr(1),0)==77,"invalid cb does not dereference");
  IntPtr exact=Input(1);
  fakeNow=60999; Assert(Invoke(s.Code,1,exact,40)==1,"deadline minus one");
  fakeNow=61000; Assert(Invoke(s.Code,1,exact,40)==77,"exact expiry");Assert(Invoke(s.Code,1,new IntPtr(1),40)==77,"expired invalid pointer not dereferenced");
  fakeNow=999; Assert(Invoke(s.Code,1,exact,40)==77,"clock before start failopen");
  fakeNow=ulong.MaxValue; Assert(Invoke(s.Code,1,exact,40)==77,"large time failopen");
  Marshal.FreeHGlobal(exact);
  bool rejected=false; try{new Stub(0,60001,false);}catch(Exception){rejected=true;} Assert(rejected,"overlong TTL rejected");
  Assert(Interlocked.Read(ref abiErrors)==0,"forwarded original arguments");
  Console.WriteLine("POLICY 2048 structures + boundary/null/ABI checks PASS");
 }
 static void Concurrency() {
  fakeNow=1000; Stub s=new Stub(1000,60000,false);
  using(Slot slot=new Slot("Local\\IAT_"+Guid.NewGuid().ToString("N"),true)) {
   slot.Initialize();slot.ReadOnly(); Assert(slot.SwapProtected(s.Code.ToInt64(),originalPtr.ToInt64()),"install CAS");
   Assert(!slot.SwapProtected(s.Code.ToInt64(),0),"ownership mismatch rejected");
   blockClock=true; entered.Reset();release.Reset();
   Task<uint> pending=Task.Factory.StartNew(delegate {IntPtr p=Input(1); try{return Invoke(new IntPtr(slot.Read()),1,p,40);}finally{Marshal.FreeHGlobal(p);}});
   Assert(entered.WaitOne(3000),"pending call entered stub");
   Assert(slot.SwapProtected(originalPtr.ToInt64(),s.Code.ToInt64()),"restore while pending");
   release.Set(); Assert(pending.Wait(3000),"pending finishes"); Assert(pending.Result==1,"already entered active call safely finishes");blockClock=false;
   IntPtr exact=Input(1); Assert(Invoke(new IntPtr(slot.Read()),1,exact,40)==77,"post restore original");
   fakeNow=61000; Assert(Invoke(s.Code,1,exact,40)==77,"cached old pointer expired passes");Marshal.FreeHGlobal(exact);
   fakeNow=1000;Assert(slot.SwapProtected(s.Code.ToInt64(),originalPtr.ToInt64()),"stress install");
   long failures=0;
   Task[] workers=new Task[8];Barrier stressReady=new Barrier(9),stressResume=new Barrier(9);
   for(int i=0;i<workers.Length;i++)workers[i]=Task.Factory.StartNew(delegate {
    IntPtr p=Input(1);try{for(int j=0;j<10000;j++){if(j==5000){Check(stressReady.SignalAndWait(8000),"stress ready barrier");Check(stressResume.SignalAndWait(8000),"stress resume barrier");}uint r=Invoke(new IntPtr(slot.Read()),1,p,40);if(r!=(j<5000?1u:77u))Interlocked.Increment(ref failures);}}finally{Marshal.FreeHGlobal(p);}
   });
   Assert(stressReady.SignalAndWait(8000),"all workers completed active half");Assert(slot.SwapProtected(originalPtr.ToInt64(),s.Code.ToInt64()),"stress restore");Assert(stressResume.SignalAndWait(8000),"workers released after restore");
   Assert(Task.WaitAll(workers,10000),"stress completes");Assert(failures==0,"40000 suppression + 40000 forward valid");stressReady.Dispose();stressResume.Dispose();
  }
  Assert(Interlocked.Read(ref abiErrors)==0,"multithread ABI"); Console.WriteLine("CONCURRENCY pending restore + 8x10000 PASS");
 }
 static Process Spawn(string args) {return Process.Start(new ProcessStartInfo {FileName=Process.GetCurrentProcess().MainModule.FileName, Arguments=args,UseShellExecute=false,CreateNoWindow=true});}
 static int Watchdog(string[] a) {
  string name=a[1];ulong deadline=ulong.Parse(a[2]);long expected=long.Parse(a[3]),orig=long.Parse(a[4]);string report=a[5];
  using(Slot slot=new Slot(name,false)) {
   // The watchdog never executes any victim pointer and allocates no victim memory.
   while(GetTickCount64()<deadline)Thread.Sleep(10);
   long previous=slot.CAS(orig,expected);
   Check(previous==expected,"watchdog ownership CAS");Check(slot.Read()==orig,"watchdog exact readback");
   File.WriteAllText(report,"WATCHDOG restored via shared aligned CAS at="+GetTickCount64()+" deadline="+deadline);
  }return 0;
 }
 static int Victim(string[] a) {
  string dir=a[1],token=a[2];string name="Local\\IAT_"+token; string report=Path.Combine(dir,token+".report"),wd=Path.Combine(dir,token+".watchdog");
  ulong start=GetTickCount64();Stub s=new Stub(start,2000,true);
  using(Slot slot=new Slot(name,true)) {
   slot.Initialize();slot.ReadOnly();Check(slot.SwapProtected(s.Code.ToInt64(),originalPtr.ToInt64()),"victim install");
   Process watchdog=Spawn("--watchdog "+name+" "+(start+5000)+" "+s.Code.ToInt64()+" "+originalPtr.ToInt64()+" \""+wd+"\"");
   IntPtr p=Input(1);Check(Invoke(new IntPtr(slot.Read()),1,p,40)==1,"initial active");File.WriteAllText(Path.Combine(dir,token+".ready"),"victim="+Process.GetCurrentProcess().Id+" watchdog="+watchdog.Id);
   while(GetTickCount64()<start+2100)Thread.Sleep(10);
   ulong expiryObserved=GetTickCount64();Check(slot.Read()==s.Code.ToInt64(),"IAT still stub before watchdog");Check(Invoke(new IntPtr(slot.Read()),1,p,40)==77,"self expiry independent watchdog");
   Check(watchdog.WaitForExit(5000),"watchdog finishes bounded");Check(watchdog.ExitCode==0,"watchdog exit");
   Check(slot.Read()==originalPtr.ToInt64(),"victim readback original");Check(Invoke(new IntPtr(slot.Read()),1,p,40)==77,"restored call");
   Check(GetTickCount64()<=start+90000,"watchdog readback within 90s fixture budget");Marshal.FreeHGlobal(p);File.WriteAllText(report,"ORPHAN victim PASS TTL=2000ms expiryObserved="+(expiryObserved-start)+"ms; watchdog due5000ms; original readback; retained one RX page\n"+File.ReadAllText(wd));
  }return 0;
 }
 static void OrphanTest() {
  string dir=AppDomain.CurrentDomain.BaseDirectory, token=Guid.NewGuid().ToString("N");
  Process controller=Spawn("--controller \""+dir.TrimEnd('\\')+"\" "+token);
  string ready=Path.Combine(dir,token+".ready");Stopwatch readyWait=Stopwatch.StartNew();while(!File.Exists(ready)&&readyWait.ElapsedMilliseconds<3000)Thread.Sleep(10);
  Assert(File.Exists(ready),"victim ready before kill");Assert(!controller.HasExited,"controller still live");controller.Kill();
  Assert(controller.WaitForExit(3000),"controller exited");Assert(controller.ExitCode!=0,"controller abrupt death exit code");Console.WriteLine("CONTROLLER abrupt kill PASS pid="+controller.Id+" exit="+controller.ExitCode);
  string report=Path.Combine(dir,token+".report");Stopwatch wait=Stopwatch.StartNew();while(!File.Exists(report)&&wait.ElapsedMilliseconds<10000)Thread.Sleep(50);
  Assert(File.Exists(report),"orphan report");Console.WriteLine(File.ReadAllText(report));
 }
 static void Mitigations() {
  ulong dep;bool depOK=GetProcessMitigationPolicy(GetCurrentProcess(),0,out dep,8);Check(depOK,"DEP query");Console.WriteLine("MITIGATION DEP flags=0x"+dep.ToString("X"));
  foreach(int kind in new int[]{2,7,16}) {uint flags;bool ok=GetProcessMitigationPolicyDword(GetCurrentProcess(),kind,out flags,4);Check(ok,"mitigation query "+kind);Console.WriteLine("MITIGATION kind="+kind+" flags=0x"+flags.ToString("X"));}
 }
 public static int Main(string[] a) {
  try {
   if(a.Length>0&&a[0]=="--watchdog")return Watchdog(a);
   if(a.Length>0&&a[0]=="--victim")return Victim(a);
   if(a.Length>0&&a[0]=="--controller") {Spawn("--victim \""+a[1]+"\" "+a[2]);Thread.Sleep(Timeout.Infinite);return 0;}
   Check(IntPtr.Size==8,"x64 only");Mitigations();Policy();Concurrency();OrphanTest();
   Console.WriteLine("PASS checks="+checks+" ABI_ERRORS="+abiErrors+" mock_calls="+calls+" no user input; no service access");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
