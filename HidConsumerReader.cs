using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
public sealed class HidConsumerFrame { public string Device; public RemoteInputEvent Input; public long Tick; }
// One nonexclusive Consumer-only HID file owns its own HIDClass input queue.
public sealed class HidConsumerReader : IDisposable {
 [StructLayout(LayoutKind.Sequential)]struct DI {public int size;public Guid cls;public int flags;public IntPtr reserved;}
 [StructLayout(LayoutKind.Sequential)]struct Attr {public int size;public ushort vid,pid,ver;}
 [StructLayout(LayoutKind.Sequential)]struct Caps {public ushort usage,page,input,output,feature;[MarshalAs(UnmanagedType.ByValArray,SizeConst=17)]public ushort[] reserved;public ushort links,ib,iv,id,ob,ov,od,fb,fv,fd;}
 [StructLayout(LayoutKind.Sequential)]struct RID {public ushort page,usage;public uint flags;public IntPtr hwnd;}
 [StructLayout(LayoutKind.Sequential)]struct Header {public uint type,size;public IntPtr device,wparam;}
 [StructLayout(LayoutKind.Sequential)]struct OL {public IntPtr low,high;public uint offset,offsetHigh;public IntPtr ev;}
 [DllImport("hid.dll")]static extern void HidD_GetHidGuid(out Guid g);
 [DllImport("hid.dll")]static extern bool HidD_GetAttributes(SafeFileHandle h,ref Attr a);
 [DllImport("hid.dll")]static extern bool HidD_GetPreparsedData(SafeFileHandle h,out IntPtr p);
 [DllImport("hid.dll")]static extern bool HidD_FreePreparsedData(IntPtr p);
 [DllImport("hid.dll")]static extern int HidP_GetCaps(IntPtr p,out Caps c);
 [DllImport("hid.dll")]static extern bool HidD_GetNumInputBuffers(SafeFileHandle h,out uint n);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr SetupDiGetClassDevs(ref Guid g,string e,IntPtr w,uint flags);
 [DllImport("setupapi.dll",SetLastError=true)]static extern bool SetupDiEnumDeviceInterfaces(IntPtr h,IntPtr info,ref Guid g,uint i,ref DI d);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr h,ref DI d,IntPtr detail,uint size,out uint need,IntPtr info);
 [DllImport("setupapi.dll")]static extern bool SetupDiDestroyDeviceInfoList(IntPtr h);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadFile(SafeFileHandle h,IntPtr data,uint count,IntPtr bytes,IntPtr ol);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetOverlappedResult(SafeFileHandle h,IntPtr ol,out uint bytes,bool wait);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool CancelIoEx(SafeFileHandle h,IntPtr ol);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateEvent(IntPtr sa,bool manual,bool initial,string name);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr h,uint ms);
 [DllImport("kernel32.dll")]static extern bool ResetEvent(IntPtr h);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 static bool IsTarget(string p){return ConsumerInput.IsTarget(p)&&p.IndexOf("&COL02",StringComparison.OrdinalIgnoreCase)>=0;}
 static string Find(){Guid g;HidD_GetHidGuid(out g);IntPtr set=SetupDiGetClassDevs(ref g,null,IntPtr.Zero,18);if(set==new IntPtr(-1))throw new Exception("SetupDi "+Marshal.GetLastWin32Error());string found=null;try{for(uint i=0;;i++){DI d=new DI();d.size=Marshal.SizeOf(typeof(DI));if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref g,i,ref d)){if(Marshal.GetLastWin32Error()!=259)throw new Exception("Enum "+Marshal.GetLastWin32Error());break;}uint n;SetupDiGetDeviceInterfaceDetail(set,ref d,IntPtr.Zero,0,out n,IntPtr.Zero);IntPtr p=Marshal.AllocHGlobal((int)n);try{Marshal.WriteInt32(p,IntPtr.Size==8?8:6);if(!SetupDiGetDeviceInterfaceDetail(set,ref d,p,n,out n,IntPtr.Zero))throw new Exception("Detail "+Marshal.GetLastWin32Error());string path=Marshal.PtrToStringUni(IntPtr.Add(p,4));if(!IsTarget(path))continue;if(found!=null)throw new Exception("multiple target Consumer interfaces");found=path;}finally{Marshal.FreeHGlobal(p);}}}finally{SetupDiDestroyDeviceInfoList(set);}if(found==null)throw new Exception("target Consumer interface absent");return found;}
 static Caps Identify(SafeFileHandle h){Attr a=new Attr();a.size=Marshal.SizeOf(typeof(Attr));if(!HidD_GetAttributes(h,ref a))throw new Exception("Attributes "+Marshal.GetLastWin32Error());IntPtr p;if(!HidD_GetPreparsedData(h,out p))throw new Exception("Preparsed "+Marshal.GetLastWin32Error());Caps c;try{if(HidP_GetCaps(p,out c)!=0x00110000)throw new Exception("Caps failed");}finally{HidD_FreePreparsedData(p);}if(c.page!=12||c.usage!=1||c.input!=5)throw new Exception("Consumer-only guard");return c;}

 readonly object gate=new object(); readonly Queue<HidConsumerFrame> queue=new Queue<HidConsumerFrame>();
 readonly ManualResetEvent stop=new ManualResetEvent(false); Thread thread; SafeFileHandle handle;
 volatile bool active,disposed; public bool Active {get{return active&&!disposed;}}
 public int Pending {get{lock(gate)return queue.Count;}}
 public HidConsumerReader(bool start){if(start){thread=new Thread(Run);thread.IsBackground=true;thread.Start();}}
 public bool Offer(string path,byte[] report){if(disposed||!IsTarget(path))return false;int code=ConsumerInput.Decode(path,report);if(code<0)return false;lock(gate){if(disposed)return false;if(queue.Count>=128){queue.Clear();queue.Enqueue(new HidConsumerFrame{Device=path,Input=new RemoteInputEvent{InputType="Consumer",IsRelease=true},Tick=Stopwatch.GetTimestamp()});return false;}queue.Enqueue(new HidConsumerFrame{Device=path,Input=new RemoteInputEvent{InputType="Consumer",Code=(ushort)code,IsRelease=code==0},Tick=Stopwatch.GetTimestamp()});return true;}}
 public HidConsumerFrame Take(){lock(gate){return queue.Count==0?null:queue.Dequeue();}}
 void Run(){while(!stop.WaitOne(0)){try{string path=Find();using(var h=CreateFile(path,0x80000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero)){if(h.IsInvalid)throw new InvalidOperationException("HID read open");var caps=Identify(h);lock(gate)handle=h;Read(h,path,caps.input);}}catch(Exception){ }finally{active=false;lock(gate)handle=null;}if(!stop.WaitOne(1000))continue;break;}}
 void Read(SafeFileHandle h,string path,int len){IntPtr buf=Marshal.AllocHGlobal(len),ol=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OL))),ev=CreateEvent(IntPtr.Zero,true,false,null);try{if(ev==IntPtr.Zero)throw new InvalidOperationException("HID event");while(!stop.WaitOne(0)){ResetEvent(ev);var o=new OL();o.ev=ev;Marshal.StructureToPtr(o,ol,false);bool ok=ReadFile(h,buf,(uint)len,IntPtr.Zero,ol);int error=ok?0:Marshal.GetLastWin32Error();if(!ok&&error!=997)break;active=true;if(!ok){while(!stop.WaitOne(0)&&WaitForSingleObject(ev,100)==258){}if(stop.WaitOne(0))CancelIoEx(h,ol);}uint n;if(!GetOverlappedResult(h,ol,out n,true))break;if(stop.WaitOne(0))break;if(n==len){var bytes=new byte[len];Marshal.Copy(buf,bytes,0,len);Offer(path,bytes);}}}finally{active=false;CloseHandle(ev);Marshal.FreeHGlobal(ol);Marshal.FreeHGlobal(buf);}}
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;stop.Set();if(handle!=null&&!handle.IsInvalid)CancelIoEx(handle,IntPtr.Zero);}if(thread!=null)thread.Join();lock(gate)queue.Clear();stop.Dispose();}
}
