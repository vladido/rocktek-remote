using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
public interface IActionAdapter {bool Activate(string path);void Launch(string path,string arguments,string directory);void Open(string target);void Key(string combination);void Mouse(string action,int amount);}
public sealed class ActionExecutor : IDisposable {
 readonly IActionAdapter adapter;readonly Action<string> log;readonly object gate=new object();Thread worker;CancellationTokenSource cancellation;bool disposed;
 public ActionExecutor(IActionAdapter a,Action<string> logger){adapter=a;log=logger;}
 public bool Busy {get{lock(gate)return worker!=null;}}
 public bool TryExecute(ActionSpec action,string settings){return TryExecute(action,settings,null);}
 public bool TryExecute(ActionSpec action,string settings,Func<bool> permitted){ActionValidation.Validate(action,false);var copy=new JavaScriptSerializer().Deserialize<ActionSpec>(new JavaScriptSerializer().Serialize(action));lock(gate){if(disposed||worker!=null)return false;cancellation=new CancellationTokenSource();var token=cancellation.Token;worker=new Thread(()=>{try{Execute(copy,settings,token,permitted);}catch(OperationCanceledException){}catch(Exception e){SafeLog("ACTION_ERROR "+e.Message);}finally{lock(gate){worker=null;cancellation.Dispose();cancellation=null;}}});worker.IsBackground=true;worker.Name="RockTek actions";worker.Start();return true;}}
 void SafeLog(string value){try{if(log!=null)log(value.Length>512?value.Substring(0,512):value);}catch{}}
 public void Cancel(){lock(gate){if(cancellation!=null)cancellation.Cancel();}}
 public bool WaitForIdle(int timeout){Thread t;lock(gate)t=worker;return t==null||t.Join(timeout);}
 void Execute(ActionSpec a,string settings,CancellationToken token,Func<bool> permitted){token.ThrowIfCancellationRequested();if(permitted!=null&&!permitted())throw new InvalidOperationException(UiText.Get("Целевое приложение больше не активно; оставшиеся шаги отменены."));switch(a.Action){case "OpenTargetApplication":case "LaunchApplication":string p=ActionValidation.ResolvePath(a.ApplicationPath,settings);string d=String.IsNullOrWhiteSpace(a.WorkingDirectory)?Path.GetDirectoryName(p):ActionValidation.ResolvePath(a.WorkingDirectory,settings);if(!adapter.Activate(p))adapter.Launch(p,a.Arguments??"",d);break;case "OpenTarget":adapter.Open(ActionValidation.OpenTarget(a.Target,settings));break;case "Key":adapter.Key(a.Key);break;case "Mouse":adapter.Mouse(a.MouseAction,a.ScrollAmount);break;case "Wait":if(token.WaitHandle.WaitOne(a.WaitMilliseconds))token.ThrowIfCancellationRequested();break;case "Sequence":foreach(var s in a.Steps)Execute(s,settings,token,permitted);break;}token.ThrowIfCancellationRequested();}
 public void Dispose(){lock(gate){disposed=true;if(cancellation!=null)cancellation.Cancel();}WaitForIdle(1000);}
}
public static class NativeActionInput {
 public const uint Sentinel=0x52544B47u;
 [StructLayout(LayoutKind.Sequential)]public struct KeyboardPacket{public ushort Vk,Scan;public uint Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Sequential)]public struct MousePacket{public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Explicit)]public struct Union{[FieldOffset(0)]public KeyboardPacket Key;[FieldOffset(0)]public MousePacket Mouse;}
 [StructLayout(LayoutKind.Sequential)]public struct Packet{public uint Type;public Union Data;}
 [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint n,Packet[] packets,int size);
 static Packet Key(ushort key,bool up){return new Packet{Type=1,Data=new Union{Key=new KeyboardPacket{Vk=key,Flags=(up?2u:0u)|((key>=0x21&&key<=0x2E)||key==91?1u:0u),Extra=new UIntPtr(Sentinel)}}};}
 public static Packet[] Keys(string text){var k=KeyCombination.Parse(text);var packets=new List<Packet>();foreach(var m in k.Modifiers)packets.Add(Key(m,false));packets.Add(Key(k.Key,false));packets.Add(Key(k.Key,true));for(int i=k.Modifiers.Length-1;i>=0;i--)packets.Add(Key(k.Modifiers[i],true));return packets.ToArray();}
 public static Packet[] Mouse(string action,int amount){ActionValidation.Validate(new ActionSpec{Action="Mouse",MouseAction=action,ScrollAmount=amount},true);uint[] flags=action=="LeftClick"?new uint[]{2,4}:action=="RightClick"?new uint[]{8,16}:action=="DoubleClick"?new uint[]{2,4,2,4}:new uint[]{0x800};var packets=new List<Packet>();foreach(uint f in flags)packets.Add(new Packet{Type=0,Data=new Union{Mouse=new MousePacket{Flags=f,Data=f==0x800?unchecked((uint)(amount*120)):0,Extra=new UIntPtr(Sentinel)}}});return packets.ToArray();}
 public static void Send(Packet[] packets){uint n=SendInput((uint)packets.Length,packets,Marshal.SizeOf(typeof(Packet)));if(n!=packets.Length)throw new InvalidOperationException("SendInput: "+n+"/"+packets.Length+UiText.Get(", ошибка ")+Marshal.GetLastWin32Error());}
}
public sealed class WindowsActionAdapter : IActionAdapter {
 delegate bool EnumProc(IntPtr h,IntPtr l);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc callback,IntPtr data);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 public bool Activate(string path){IntPtr window=IntPtr.Zero;EnumWindows((h,l)=>{uint id;GetWindowThreadProcessId(h,out id);try{using(var p=Process.GetProcessById((int)id)){if(p.SessionId==Process.GetCurrentProcess().SessionId&&IsWindowVisible(h)&&String.Equals(Installation.ProcessPath((int)id),path,StringComparison.OrdinalIgnoreCase)){window=h;return false;}}}catch{}return true;},IntPtr.Zero);if(window!=IntPtr.Zero){ShowWindow(window,9);if(!SetForegroundWindow(window))throw new InvalidOperationException(UiText.Get("Windows отказала в переключении окна приложения."));return true;}return false;}

 public void Launch(string path,string arguments,string directory){if(!File.Exists(path))throw new FileNotFoundException(UiText.Get("Приложение не найдено."));if(!Directory.Exists(directory))throw new DirectoryNotFoundException(UiText.Get("Рабочая папка не найдена."));using(var p=Process.Start(new ProcessStartInfo(path,arguments){UseShellExecute=false,WorkingDirectory=directory})){} }
 public void Open(string target){if(!target.StartsWith("http://",StringComparison.OrdinalIgnoreCase)&&!target.StartsWith("https://",StringComparison.OrdinalIgnoreCase)&&!File.Exists(target)&&!Directory.Exists(target))throw new FileNotFoundException(UiText.Get("Файл или папка не найдены."));using(var p=Process.Start(new ProcessStartInfo(target){UseShellExecute=true})){} }
 public void Key(string value){NativeActionInput.Send(NativeActionInput.Keys(value));}
 public void Mouse(string action,int amount){NativeActionInput.Send(NativeActionInput.Mouse(action,amount));}
}
