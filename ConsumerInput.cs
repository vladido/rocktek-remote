using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
// Monotonic, bounded correlation; no device-less packet acquires an identity.
public sealed class WakeHomeRouter {
 long offTick,on=-10000,raw=-10000,dispatch=-10000;bool off,armed,consumed;
 public void Display(int state,long now){if(state==0){offTick=now;off=true;armed=true;consumed=false;on=raw=dispatch=-10000;}else if(state==1&&off){off=false;on=now;}}
 bool Window(long now){return armed&&now>=offTick&&(off||now<=on+2000);}
 public void RawHome(long now){if(Window(now))raw=now;}
 // 0 wait, 1 replay ordinary BrowserHome, 2 saved Home, 3 already handled.
 public int Resolve(long hook,long now,bool present){
  if(now-hook<120)return 0;
  bool wake=Window(hook)&&Window(now);
  // WH_KEYBOARD_LL suppression prevents original keyboard WM_INPUT delivery.
  // Device-less VKAC packets from our replay cannot prove the wake press.
  // Approved policy: unmatched BrowserHome within a dark wake uses saved Home,
  // including ordinary keyboard BrowserHome in this narrow wake interval.
  if(wake&&!off){if(consumed)return 3;if(!present)return 1;consumed=true;dispatch=now;return 2;}
  if(wake&&!consumed&&now-hook<2000)return 0;
  return 1;
 }
 public string Snapshot(long now){return "wakeHomeState off="+off+" armed="+armed+" consumed="+consumed+" sinceDisplayOn="+(on<0?-1:now-on)+" sinceZeroRaw="+(raw<0?-1:now-raw);}
 public bool SuppressTarget(long now){return dispatch>=0&&now-dispatch>=0&&now-dispatch<=2000;}
 public static bool IsZeroHome(uint type,byte[] data){if(type!=1||data==null||data.Length!=16)return false;ushort f=BitConverter.ToUInt16(data,2);return BitConverter.ToUInt16(data,6)==0xAC&&BitConverter.ToUInt16(data,0)!=255&&(f&~7)==0&&(f&1)==0;}
}
// One owner thread holds an abandoned-safe mutex. No persistent marker or sticky event.
public sealed class LearnGate:IDisposable {
 const string Name="Local\\RockTekRemote-Learn";
 readonly ManualResetEvent stop=new ManualResetEvent(false),ready=new ManualResetEvent(false);
 Thread thread;bool acquired;bool disposed;
 public LearnGate(){thread=new Thread(()=>{using(var m=new Mutex(false,Name)){try{acquired=m.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}ready.Set();if(acquired){stop.WaitOne();m.ReleaseMutex();}}});thread.IsBackground=true;thread.Start();ready.WaitOne();if(!acquired){Dispose();throw new InvalidOperationException(UiText.Get("Обучение уже открыто в другом окне."));}}
 public static bool IsLearning(){using(var m=new Mutex(false,Name)){bool owned;try{owned=m.WaitOne(0);}catch(AbandonedMutexException){owned=true;}if(owned)m.ReleaseMutex();return !owned;}}
 public void Dispose(){if(disposed)return;disposed=true;stop.Set();thread.Join();stop.Dispose();ready.Dispose();}
}
// Identity is source + code; scan metadata is diagnostic, not a separate mapping.
public sealed class RemoteInputEvent {
 public string InputType; public ushort Code,ScanCode,ScanFlags; public bool IsRelease;
 public string Description {get{return InputType=="Keyboard"?"Keyboard · "+((System.Windows.Forms.Keys)Code).ToString()+" (VK 0x"+Code.ToString("X4")+", scan 0x"+ScanCode.ToString("X4")+")":"Consumer · usage 0x"+Code.ToString("X4");}}
}
public static class ConsumerInput {
 public static string Target {get{return DeviceFilter.Target;}}
 [StructLayout(LayoutKind.Sequential)]struct RID{public ushort Page,Usage;public uint Flags;public IntPtr Window;}
 [StructLayout(LayoutKind.Sequential)]struct Header{public uint Type,Size;public IntPtr Device,WParam;}
 [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterRawInputDevices(RID[] d,uint n,uint size);
 [DllImport("user32.dll")]static extern uint GetRawInputData(IntPtr h,uint command,IntPtr data,ref uint size,uint header);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint GetRawInputDeviceInfo(IntPtr h,uint command,StringBuilder data,ref uint size);
 [StructLayout(LayoutKind.Sequential)]struct DeviceList{public IntPtr Device;public uint Type;}
 [DllImport("user32.dll",SetLastError=true)]static extern uint GetRegisteredRawInputDevices([Out]RID[] devices,ref uint count,uint size);
 [DllImport("user32.dll",SetLastError=true)]static extern uint GetRawInputDeviceList([Out]DeviceList[] devices,ref uint count,uint size);
 [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
 public static string RegistrationSnapshot(IntPtr window){
  uint count=0,size=(uint)Marshal.SizeOf(typeof(RID));GetRegisteredRawInputDevices(null,ref count,size);if(count>128)return "registration-count-invalid";
  var registered=new RID[count];uint got=count==0?0:GetRegisteredRawInputDevices(registered,ref count,size);var text=new StringBuilder("hwnd="+window.ToInt64().ToString("X")+" valid="+IsWindow(window)+" registrations="+got);
  if(got!=uint.MaxValue)foreach(var r in registered)if((r.Page==12&&r.Usage==1)||(r.Page==1&&r.Usage==6))text.Append(" [page="+r.Page+" usage="+r.Usage+" flags="+r.Flags.ToString("X")+" sink="+r.Window.ToInt64().ToString("X")+" valid="+IsWindow(r.Window)+"]");
  uint devices=0,ds=(uint)Marshal.SizeOf(typeof(DeviceList));GetRawInputDeviceList(null,ref devices,ds);if(devices>1024)return text+" device-count-invalid";var list=new DeviceList[devices];if(GetRawInputDeviceList(list,ref devices,ds)==uint.MaxValue)return text+" enumeration-error="+Marshal.GetLastWin32Error();
  int targets=0;foreach(var d in list){uint n=0;GetRawInputDeviceInfo(d.Device,0x20000007,null,ref n);if(n==0||n>32768)continue;var name=new StringBuilder((int)n+1);if(GetRawInputDeviceInfo(d.Device,0x20000007,name,ref n)==uint.MaxValue||!IsTarget(name.ToString()))continue;targets++;text.Append(" [target-handle="+d.Device.ToInt64().ToString("X")+" type="+d.Type+"]");}text.Append(" targetCollections="+targets);return text.ToString();
 }
 public static bool IsTarget(string device){return DeviceFilter.IsTarget(device);}
 [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW",SetLastError=true)]static extern uint GetDeviceInfo(IntPtr h,uint command,IntPtr data,ref uint size);
 public static bool TargetConsumerPresent(){uint n=0,s=(uint)Marshal.SizeOf(typeof(DeviceList));if(GetRawInputDeviceList(null,ref n,s)==uint.MaxValue||n>1024)return false;var list=new DeviceList[n];if(GetRawInputDeviceList(list,ref n,s)==uint.MaxValue)return false;foreach(var d in list){if(d.Type!=2)continue;uint chars=0;if(GetRawInputDeviceInfo(d.Device,0x20000007,null,ref chars)==uint.MaxValue||chars==0||chars>32768)continue;var name=new StringBuilder((int)chars+1);if(GetRawInputDeviceInfo(d.Device,0x20000007,name,ref chars)==uint.MaxValue||!IsTarget(name.ToString()))continue;IntPtr info=Marshal.AllocHGlobal(32);try{Marshal.WriteInt32(info,32);uint bytes=32;if(GetDeviceInfo(d.Device,0x2000000B,info,ref bytes)!=uint.MaxValue&&Marshal.ReadInt32(info,4)==2&&Marshal.ReadInt16(info,20)==12&&Marshal.ReadInt16(info,22)==1)return true;}finally{Marshal.FreeHGlobal(info);}}return false;}
 public static int Decode(string device,byte[] report){if(!IsTarget(device)||report==null||report.Length!=5||report[0]!=2)return -1;return report[1]|report[2]<<8;}
 public static RemoteInputEvent DecodeKeyboard(string device,byte[] data){
  if(!IsTarget(device)||data==null||data.Length!=16)return null;
  ushort scan=BitConverter.ToUInt16(data,0),flags=BitConverter.ToUInt16(data,2),vk=BitConverter.ToUInt16(data,6);
  if(vk==0||vk>=255||scan==255||(flags&~7)!=0)return null;
  return new RemoteInputEvent{InputType="Keyboard",Code=vk,ScanCode=scan,ScanFlags=(ushort)(flags&6),IsRelease=(flags&1)!=0};
 }
 public static void Register(IntPtr window,bool remove){
  var d=new[]{new RID{Page=12,Usage=1,Flags=remove?1u:0x100u,Window=remove?IntPtr.Zero:window},new RID{Page=1,Usage=6,Flags=remove?1u:0x100u,Window=remove?IntPtr.Zero:window}};
  if(!RegisterRawInputDevices(d,(uint)d.Length,(uint)Marshal.SizeOf(typeof(RID))))throw new InvalidOperationException("Raw Input: "+Marshal.GetLastWin32Error());
 }
 // Shared packet boundary used by native WM_INPUT and deterministic regression tests.
 public static void ReadPacket(string device,uint type,byte[] payload,Action<string,RemoteInputEvent> receive){
  if(!IsTarget(device)||payload==null)return;
  if(type==1){var e=DecodeKeyboard(device,payload);if(e!=null)receive(device,e);return;}
  if(type!=2||payload.Length<8)return;
  int len=BitConverter.ToInt32(payload,0),count=BitConverter.ToInt32(payload,4);
  if(len!=5||count<1||(long)len*count>payload.Length-8)return;
  for(int i=0;i<count;i++){var report=new byte[5];Array.Copy(payload,8+i*5,report,0,5);int u=Decode(device,report);if(u>=0)receive(device,new RemoteInputEvent{InputType="Consumer",Code=(ushort)u,IsRelease=u==0});}
 }
 // Fixed aggregate categories only: never names, key codes, or event-level ordinary keyboard metadata.
 static readonly string[] IntakeReasons={"query","read","short","type","size","keyboard-size","deviceless","name-size","name-unavailable","non-target","target"};
 static readonly long[] IntakeCounts=new long[IntakeReasons.Length];
 public static void RecordIntake(string reason){int i=Array.IndexOf(IntakeReasons,reason);if(i<0)throw new ArgumentException("Unknown intake category");Interlocked.Increment(ref IntakeCounts[i]);}
 public static string IntakeSnapshot(){var s=new StringBuilder("intakeAggregate");for(int i=0;i<IntakeReasons.Length;i++)s.Append(" "+IntakeReasons[i]+"="+Interlocked.Read(ref IntakeCounts[i]));return s.ToString();}
 public static string HeaderRejection(uint type,uint size,uint got,uint hs){if(got<hs+8)return "short";if(type!=1&&type!=2)return "type";if(size!=got)return "size";if(type==1&&got!=hs+16)return "keyboard-size";return "";}
 static long ZeroAcMakes,ZeroAcBreaks,ZeroAcDownMessage,ZeroAcUpMessage,ZeroAcOtherMessage,ZeroAcSentinel,ZeroAcUnexpectedFlags;
 public static string ZeroHomeSnapshot(){return "zeroVKACAggregate makes="+Interlocked.Read(ref ZeroAcMakes)+" breaks="+Interlocked.Read(ref ZeroAcBreaks)+" msgDown="+Interlocked.Read(ref ZeroAcDownMessage)+" msgUp="+Interlocked.Read(ref ZeroAcUpMessage)+" msgOther="+Interlocked.Read(ref ZeroAcOtherMessage)+" replaySentinel="+Interlocked.Read(ref ZeroAcSentinel)+" unexpectedFlags="+Interlocked.Read(ref ZeroAcUnexpectedFlags);}
 // Same constructed RAWINPUT header boundary used by native reader and tests.
 public static bool ReadZeroHomePacket(IntPtr packet,uint got,bool diagnostic){uint hs=(uint)Marshal.SizeOf(typeof(Header));if(packet==IntPtr.Zero||got<hs+16)return false;var h=(Header)Marshal.PtrToStructure(packet,typeof(Header));if(h.Device!=IntPtr.Zero||HeaderRejection(h.Type,h.Size,got,hs)!=""||h.Type!=1)return false;var zero=new byte[16];Marshal.Copy(IntPtr.Add(packet,(int)hs),zero,0,16);if(BitConverter.ToUInt16(zero,6)!=0xAC)return false;if(diagnostic){ushort flags=BitConverter.ToUInt16(zero,2);uint msg=BitConverter.ToUInt32(zero,8);if((flags&1)==0)Interlocked.Increment(ref ZeroAcMakes);else Interlocked.Increment(ref ZeroAcBreaks);if(msg==0x100||msg==0x104)Interlocked.Increment(ref ZeroAcDownMessage);else if(msg==0x101||msg==0x105)Interlocked.Increment(ref ZeroAcUpMessage);else Interlocked.Increment(ref ZeroAcOtherMessage);if(BitConverter.ToUInt32(zero,12)==0x52544B47u)Interlocked.Increment(ref ZeroAcSentinel);if((flags&~7)!=0)Interlocked.Increment(ref ZeroAcUnexpectedFlags);}return WakeHomeRouter.IsZeroHome(h.Type,zero);}
 public static void ReadEvents(IntPtr raw,Action<string,RemoteInputEvent> receive,Action<string,uint,byte[]> observe=null,bool diagnostic=false,Action zeroHome=null){
  Action<string> reject=reason=>{if(diagnostic)RecordIntake(reason);};
  uint n=0,hs=(uint)Marshal.SizeOf(typeof(Header));
  if(GetRawInputData(raw,0x10000003,IntPtr.Zero,ref n,hs)==uint.MaxValue||n<hs+8||n>1048576){reject("query");return;}
  IntPtr p=Marshal.AllocHGlobal((int)n);try{
   uint capacity=n,got=GetRawInputData(raw,0x10000003,p,ref n,hs);if(got==uint.MaxValue||got>capacity||got<hs+8){reject("read");return;}
   var h=(Header)Marshal.PtrToStructure(p,typeof(Header));string rejected=HeaderRejection(h.Type,h.Size,got,hs);if(rejected!=""){reject(rejected);return;}
   if(h.Device==IntPtr.Zero){reject("deviceless");bool ac=ReadZeroHomePacket(p,got,diagnostic);if(ac&&zeroHome!=null)zeroHome();return;}
   uint size=0;uint queried=GetRawInputDeviceInfo(h.Device,0x20000007,null,ref size);if(queried==uint.MaxValue){reject("name-unavailable");return;}if(size==0||size>32768){reject("name-size");return;}
   var name=new StringBuilder((int)size+1);if(GetRawInputDeviceInfo(h.Device,0x20000007,name,ref size)==uint.MaxValue){reject("name-unavailable");return;}
   string device=name.ToString();if(!IsTarget(device)){reject("non-target");return;}reject("target");
   var payload=new byte[got-hs];Marshal.Copy(IntPtr.Add(p,(int)hs),payload,0,payload.Length);if(observe!=null)observe(device,h.Type,payload);ReadPacket(device,h.Type,payload,receive);
  }finally{Marshal.FreeHGlobal(p);}
 }
}
