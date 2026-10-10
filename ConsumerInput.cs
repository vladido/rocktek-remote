using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
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
 public static bool IsTarget(string device){return DeviceFilter.IsTarget(device);}
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
 // Shared packet boundary used by native WM_INPUT and shared decoding.
 public static void ReadPacket(string device,uint type,byte[] payload,Action<string,RemoteInputEvent> receive){
  if(!IsTarget(device)||payload==null)return;
  if(type==1){var e=DecodeKeyboard(device,payload);if(e!=null)receive(device,e);return;}
  if(type!=2||payload.Length<8)return;
  int len=BitConverter.ToInt32(payload,0),count=BitConverter.ToInt32(payload,4);
  if(len!=5||count<1||(long)len*count>payload.Length-8)return;
  for(int i=0;i<count;i++){var report=new byte[5];Array.Copy(payload,8+i*5,report,0,5);int u=Decode(device,report);if(u>=0)receive(device,new RemoteInputEvent{InputType="Consumer",Code=(ushort)u,IsRelease=u==0});}
 }
 public static void ReadEvents(IntPtr raw,Action<string,RemoteInputEvent> receive){
  uint n=0,hs=(uint)Marshal.SizeOf(typeof(Header));
  if(GetRawInputData(raw,0x10000003,IntPtr.Zero,ref n,hs)==uint.MaxValue||n<hs+8||n>1048576)return;
  IntPtr p=Marshal.AllocHGlobal((int)n);try{
   uint capacity=n,got=GetRawInputData(raw,0x10000003,p,ref n,hs);if(got==uint.MaxValue||got>capacity||got<hs+8)return;
   var h=(Header)Marshal.PtrToStructure(p,typeof(Header));if((h.Type!=1&&h.Type!=2)||h.Size!=got)return;
   if(h.Type==1&&got!=hs+16)return;
   uint size=0;GetRawInputDeviceInfo(h.Device,0x20000007,null,ref size);if(size==0||size>32768)return;
   var name=new StringBuilder((int)size+1);if(GetRawInputDeviceInfo(h.Device,0x20000007,name,ref size)==uint.MaxValue)return;
   string device=name.ToString();if(!IsTarget(device))return;
   var payload=new byte[got-hs];Marshal.Copy(IntPtr.Add(p,(int)hs),payload,0,payload.Length);ReadPacket(device,h.Type,payload,receive);
  }finally{Marshal.FreeHGlobal(p);}
 }
}
