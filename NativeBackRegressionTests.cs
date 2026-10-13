using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
class BackFake : IActionAdapter {
 public int Keys; public bool Activate(string p){return true;} public void Launch(string p,string a,string d){} public void Open(string p){} public void Key(string k){Keys++;} public void Mouse(string a,int n){}
}
class NativeBackRegressionTests {
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static readonly Type T=typeof(RemoteConfig).Assembly.GetType("RockTekRemote");
 sealed class Fixture : IDisposable {
  public object Runtime=FormatterServices.GetUninitializedObject(T); public BackFake Fake=new BackFake(); public HidConsumerReader Reader=new HidConsumerReader(false); public List<string> Replay=new List<string>(); public Stopwatch Clock=Stopwatch.StartNew();
  public Fixture(){
   Set("Config",RemoteConfig.Defaults()); var cfg=(RemoteConfig)T.GetField("Config",F).GetValue(Runtime); cfg.Buttons["Back"].Action="Key"; cfg.Buttons["Back"].Key="P"; cfg.Buttons["Back"].Scope="Global";
   Set("Clock",Clock); Set("Repeater",new HoldRepeater()); Set("ActionAdapter",Fake); Set("HomeLock",new object()); Set("PendingHomes",new List<long>()); Set("HookLogs",new System.Collections.Concurrent.ConcurrentQueue<string>()); Set("RemoteHomeTick",(long)-10000); Set("WakeHome",new WakeHomeRouter()); Set("DirectConsumer",Reader);
   typeof(HidConsumerReader).GetField("active",F).SetValue(Reader,true);
   Set("ConfigPath",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"back-fixture.json")); Set("LogFile",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"back-fixture.log"));
   // Optional until the production feature exists: RED must fail on behavior, not reflection.
   var gate=T.GetField("NativeBack",F); if(gate!=null)gate.SetValue(Runtime,Activator.CreateInstance(gate.FieldType));
   Optional("DisplayDark",true);
   Optional("BrowserBackReplay",(Action<ushort,uint,uint,uint>)((vk,scan,flags,time)=>Replay.Add(vk+":"+scan+":"+flags+":"+time)));
  }
  public void Set(string n,object v){T.GetField(n,F).SetValue(Runtime,v);} public void Optional(string n,object v){var field=T.GetField(n,F);if(field!=null)field.SetValue(Runtime,v);}
  public IntPtr Hook(uint vk,uint flags,int msg=0x100,uint extra=0,uint scan=0x6A,uint time=123){IntPtr p=Marshal.AllocHGlobal(24);try{for(int i=0;i<24;i++)Marshal.WriteByte(p,i,0);Marshal.WriteInt32(p,0,(int)vk);Marshal.WriteInt32(p,4,(int)scan);Marshal.WriteInt32(p,8,(int)flags);Marshal.WriteInt32(p,12,(int)time);Marshal.WriteInt64(p,16,extra);return (IntPtr)T.GetMethod("KeyboardHook",F).Invoke(Runtime,new object[]{0,new IntPtr(msg),p});}finally{Marshal.FreeHGlobal(p);}}
  public void Tick(){T.GetMethod("Tick",F).Invoke(Runtime,null);var e=(ActionExecutor)T.GetField("Executor",F).GetValue(Runtime);if(e!=null)e.WaitForIdle(1000);}
  public void Back(){Reader.Offer("hid#AABBCCDDEEFF&col02#fixture",new byte[]{2,0x24,2,0,0});}
  public void Dispose(){var e=(ActionExecutor)T.GetField("Executor",F).GetValue(Runtime);if(e!=null)e.Dispose();Reader.Dispose();}
 }
 static void Check(bool b,string s){if(!b)throw new Exception("FAIL "+s);}
 [STAThread]static int Main(){Environment.SetEnvironmentVariable("ROCKTEK_REMOTE_DEVICE","AABBCCDDEEFF");try{
  using(var x=new Fixture()){
   Check(x.Hook(0xA6,17)==new IntPtr(1),"dark injected BrowserBack make must be suppressed before native wake");
   Check(x.Hook(0xA6,145,0x101)==new IntPtr(1),"captured BrowserBack break must be suppressed");
   x.Back();x.Tick();Thread.Sleep(200);x.Tick();
   Check(x.Replay.Count==0,"target direct HID 0224 must cancel native pair without original WM_INPUT"); Check(x.Fake.Keys==1,"saved Back action dispatched once");
   Console.WriteLine("PASS actual hook -> queue -> direct HID -> Tick cancels native Back pair; saved P once");
  }
  using(var x=new Fixture()){
   // Evidence sitting in the direct-read queue predates this native press by >180ms.
   x.Back();Thread.Sleep(220);x.Hook(0xA6,17);x.Hook(0xA6,145,0x101);x.Tick();Thread.Sleep(200);x.Tick();
   Check(x.Replay.Count==2,"stale queued direct HID evidence must NOT cancel a new injected BrowserBack");
   Check(x.Replay[0]=="166:106:17:123"&&x.Replay[1]=="166:106:145:123","unmatched pair preserves order, scan, flags and event time");x.Tick();Check(x.Replay.Count==2,"unmatched pair replays exactly once");
   Console.WriteLine("PASS stale queued direct HID cannot claim a newer native pair; faithful replay once");
  }
  using(var x=new Fixture()){
   x.Hook(0xA6,17);x.Back();x.Tick();x.Optional("DisplayDark",false);
   Check(x.Hook(0xA6,17)==IntPtr.Zero,"screen-on new BrowserBack make must pass even after a lost dark release");
   Check(x.Hook(0xA6,145,0x101)==IntPtr.Zero,"screen-on uncaptured break must pass");
   Console.WriteLine("PASS lost release cannot expand dark-only rule into screen-on suppression");
  }
  using(var x=new Fixture()){
   x.Optional("LearningActive",(Func<bool>)(()=>true));x.Hook(0xA6,17);x.Hook(0xA6,145,0x101);x.Back();x.Tick();Thread.Sleep(200);x.Tick();
   Check(x.Fake.Keys==0,"learning must reject mapped action via fake learning adapter");Check(x.Replay.Count==0,"learning rejection must not leak correlated native BrowserBack");
   Console.WriteLine("PASS target correlation precedes learning rejection; no lease or OS input");
  }
  using(var x=new Fixture()){
   x.Hook(0xA6,17);x.Hook(0xA6,145,0x101);x.Back();x.Tick();
   var g=T.GetField("NativeBack",F).GetValue(x.Runtime);var s=g.GetType().GetMethod("Snapshot",F);string text=s==null?"":(string)s.Invoke(g,null);
   Check(text.Contains("backCaptured=2")&&text.Contains("backMatched=1")&&text.Contains("backReplayed=0"),"bounded fixed native-Back diagnostic counters must expose cancellation without key logging");
   Console.WriteLine("PASS fixed aggregate counters, no ordinary keyboard event records");
  }
  using(var x=new Fixture()){
   Check(x.Hook(0xA6,1)==IntPtr.Zero,"ordinary noninjected BrowserBack preserved");Check(x.Hook(0xA6,129,0x101)==IntPtr.Zero,"ordinary noninjected break preserved");
   Check(x.Hook(0xA6,17,0x100,0x52544B47u)==IntPtr.Zero,"owned replay sentinel bypasses interception");
   foreach(uint vk in new uint[]{0xAD,0xAE,0xAF,0xB0,0xB1,0xB3,0x41}){Check(x.Hook(vk,17)==IntPtr.Zero,"other media/keyboard make preserved");Check(x.Hook(vk,145,0x101)==IntPtr.Zero,"other media/keyboard break preserved");}
   x.Optional("DisplayDark",false);Check(x.Hook(0xA6,17)==IntPtr.Zero,"screen-on injected BrowserBack preserved");
   x.Optional("DisplayDark",true);typeof(HidConsumerReader).GetField("active",F).SetValue(x.Reader,false);Check(x.Hook(0xA6,17)==IntPtr.Zero,"inactive direct intake preserves BrowserBack");
   x.Tick();Check(x.Replay.Count==0,"bypassed keys were not queued or replayed");Console.WriteLine("PASS ordinary BrowserBack, replay sentinel, other keys, screen-on and inactive intake guards");
  }
  using(var x=new Fixture()){
   x.Hook(0xA6,17);x.Tick();Check(x.Replay.Count==0,"no immediate fallback before deadline");Thread.Sleep(200);x.Tick();
   Check(x.Replay.Count==1,"unmatched make replays without manufacturing a break");x.Optional("DisplayDark",false);Check(x.Hook(0xA6,145,0x101)==new IntPtr(1),"late break pairs with queued make even after display transition");x.Tick();x.Tick();
   Check(x.Replay.Count==2&&x.Replay[1]=="166:106:145:123","late break replays once");Console.WriteLine("PASS unmatched make and delayed break replay separately once across display transition");
  }
  using(var x=new Fixture()){
   x.Back();x.Tick();x.Hook(0xA6,17);x.Hook(0xA6,145,0x101);x.Tick();Thread.Sleep(200);x.Tick();Check(x.Replay.Count==0,"target make preceding hook correlates");
   x.Hook(0xA6,17);x.Hook(0xA6,145,0x101);Thread.Sleep(200);x.Tick();Check(x.Replay.Count==2,"one target evidence cannot cancel two native presses");Console.WriteLine("PASS target-before-hook and single-use evidence");
  }
  foreach(string reject in new string[]{"scope","duplicate","Disabled","Native"})using(var x=new Fixture()){
   var cfg=(RemoteConfig)T.GetField("Config",F).GetValue(x.Runtime);var b=cfg.Buttons["Back"];
   if(reject=="scope"){b.Scope="Application";b.ScopeApplicationPath=@"C:\fixture-player.exe";x.Set("ForegroundPath",(Func<string>)(()=>@"C:\other.exe"));}
   else if(reject=="duplicate"){x.Set("LastUsage",(ushort)0x224);x.Set("LastSource","Consumer");}
   else b.Action=reject;
   x.Hook(0xA6,17);x.Hook(0xA6,145,0x101);x.Back();x.Tick();Thread.Sleep(200);x.Tick();Check(x.Replay.Count==0,"target correlation before "+reject+" rejection");Check(x.Fake.Keys==0,"no forbidden action for "+reject);Console.WriteLine("PASS target correlation before "+reject+" rejection");
  }
  foreach(string bad in new string[]{"release","wrong-device","keyboard","other-usage","presence-only"})using(var x=new Fixture()){
   x.Hook(0xA6,17);x.Hook(0xA6,145,0x101);var e=new RemoteInputEvent{InputType=bad=="keyboard"?"Keyboard":"Consumer",Code=(ushort)(bad=="other-usage"?0x8D:0x224),IsRelease=bad=="release"};
   var cfg=(RemoteConfig)T.GetField("Config",F).GetValue(x.Runtime);foreach(var b in cfg.Buttons.Values)b.Action="Disabled";
   if(bad!="presence-only")T.GetMethod("HandleInput",F).Invoke(x.Runtime,new object[]{bad=="wrong-device"?"hid#112233445566&col02#fixture":"hid#AABBCCDDEEFF&col02#fixture",e,false});
   else x.Set("TargetConsumerAvailable",(Func<bool>)(()=>true));
   Thread.Sleep(200);x.Tick();Check(x.Replay.Count==2,"no ownership inference from "+bad);Console.WriteLine("PASS no target ownership from "+bad);
  }
  using(var x=new Fixture()){
   int count=0;x.Optional("BrowserBackReplay",(Action<ushort,uint,uint,uint>)((vk,scan,flags,time)=>{count++;Check(x.Hook(vk,flags|16u,(flags&128u)!=0?0x101:0x100,0x52544B47u,scan,time)==IntPtr.Zero,"replayed input recursively intercepted");}));
   x.Hook(0xA6,17);x.Hook(0xA6,145,0x101);Thread.Sleep(200);x.Tick();x.Tick();Check(count==2,"replay exactly once, never recursively queued");Console.WriteLine("PASS replay callback outside hook with owned sentinel; no recursion");
  }
  using(var x=new Fixture()){
   x.Hook(0xA6,17);x.Hook(0xA6,145,0x101);
   var g=T.GetField("NativeBack",F).GetValue(x.Runtime);var pending=(System.Collections.IList)g.GetType().GetField("pending",F).GetValue(g);object press=pending[0];long tick=(long)press.GetType().GetField("Tick",F).GetValue(press);
   var drain=g.GetType().GetMethod("Drain",F);var early=(System.Collections.IList)drain.Invoke(g,new object[]{tick+179});var due=(System.Collections.IList)drain.Invoke(g,new object[]{tick+180});
   Check(early.Count==0&&due.Count==2,"unmatched decision deadline exactly 180ms, no broad wake/presence fallback");Console.WriteLine("PASS deterministic 179/180ms correlation boundary");
  }
  using(var x=new Fixture()){
   x.Hook(0xA6,17);x.Back();x.Tick();Thread.Sleep(200);
   x.Hook(0xA6,17);x.Hook(0xA6,145,0x101);Thread.Sleep(200);x.Tick();
   Check(x.Replay.Count==2,"lost dark release cannot lend old target identity to a later unmatched make");Console.WriteLine("PASS dark lost-release identity expires at the bounded make window");
  }
  return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
