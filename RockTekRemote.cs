using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Web.Script.Serialization;
class RockTekRemote : Form {
 static string Dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RockTekRemote");
 static string LogFile=Path.Combine(Dir,"actions.log");
 static bool ConfigUsesLocator; static string LocatorFile=null; string LocationWarning=""; static string ConfigPath; RemoteConfig Config; string ConfigText=""; NotifyIcon Tray; System.Drawing.Icon ownedIcon;
 void Reload(){if(ConfigUsesLocator){string warning;string selected=AppPaths.SettingsWithLocator(new string[0],LocatorFile??ConfigLocation.DefaultFile,out warning);if(warning!=LocationWarning){LocationWarning=warning;if(!String.IsNullOrEmpty(warning))Log("LOCATION_ERROR "+warning);}if(String.IsNullOrEmpty(warning)||Config==null){if(!String.Equals(ConfigPath,selected,StringComparison.OrdinalIgnoreCase)){ConfigPath=selected;ConfigText=null;ClearHeld(Clock.ElapsedMilliseconds);}}}string text=File.Exists(ConfigPath)?File.ReadAllText(ConfigPath):"";if(Config!=null&&text==ConfigText)return;if(Executor!=null)Executor.Cancel();try{var c=text==""?RemoteConfig.Defaults():RemoteConfig.Parse(text);Config=c;ConfigText=text;ClearHeld(Clock.ElapsedMilliseconds);Log("CONFIG loaded "+ConfigPath);}catch(Exception e){if(Config==null)Config=RemoteConfig.Defaults();Log("CONFIG_ERROR "+e.Message);ConfigText=text;}}
 void OpenSettings(){Process.Start(new ProcessStartInfo(typeof(RockTekRemote).Assembly.Location,(ConfigUsesLocator?"--settings-only":"--settings-only --config \""+ConfigPath+"\"")+" --language "+UiText.Language){UseShellExecute=true});}
 [StructLayout(LayoutKind.Sequential)] struct RID {public ushort Page,Usage;public uint Flags;public IntPtr Window;}
 [StructLayout(LayoutKind.Sequential)] struct Header {public uint Type,Size;public IntPtr Device,WParam;}
 [StructLayout(LayoutKind.Sequential)] struct KeyData {public uint Vk,Scan,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT {public ushort Vk,Scan;public uint Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Explicit)] struct Union { [FieldOffset(0)]public KEYBDINPUT Key;[FieldOffset(0)]public MOUSEINPUT Mouse;}
 [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Sequential)] struct INPUT {public uint Type;public Union Data;}
 delegate IntPtr HookProc(int code,IntPtr wp,IntPtr lp);
 [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterRawInputDevices(RID[] d,uint n,uint size);
 [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr h,uint command,IntPtr data,ref uint size,uint header);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(IntPtr h,uint command,StringBuilder data,ref uint size);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint n,INPUT[] data,int size);
 [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int type,HookProc callback,IntPtr mod,uint thread);
 [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h,int code,IntPtr wp,IntPtr lp);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
 IntPtr Hook;HookProc Callback;System.Threading.Thread HookThread; readonly object HomeLock=new object();System.Collections.Concurrent.ConcurrentQueue<string> HookLogs=new System.Collections.Concurrent.ConcurrentQueue<string>(); List<long> PendingHomes=new List<long>();long RemoteHomeTick=-10000; Stopwatch Clock=Stopwatch.StartNew();Dictionary<IntPtr,string> Names=new Dictionary<IntPtr,string>();
 string LastSource=""; ActionExecutor Executor; public IActionAdapter ActionAdapter=new WindowsActionAdapter(); ushort LastUsage=0; string LastAction="Ready";HoldRepeater Repeater=new HoldRepeater();string HeldDevice="";
 static readonly object LogLock=new object();static void Log(string s){lock(LogLock){try{if(s.Length>1024)s=s.Substring(0,1024);if(File.Exists(LogFile)&&new FileInfo(LogFile).Length>1048576)File.WriteAllText(LogFile,"");File.AppendAllText(LogFile,DateTime.Now.ToString("O")+" "+s+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}}}
 string DeviceName(IntPtr h){string v;if(Names.TryGetValue(h,out v))return v;uint n=0;GetRawInputDeviceInfo(h,0x20000007,null,ref n);if(n==0)return "";var b=new StringBuilder((int)n+1);GetRawInputDeviceInfo(h,0x20000007,b,ref n);v=b.ToString();Names[h]=v;return v;}
 public Func<string> ForegroundPath=null;
 string CurrentForegroundPath(){if(ForegroundPath!=null)return ForegroundPath();uint p;GetWindowThreadProcessId(GetForegroundWindow(),out p);return Installation.ProcessPath((int)p);}
 bool ScopeActive(string expected){try{return String.Equals(CurrentForegroundPath(),expected,StringComparison.OrdinalIgnoreCase);}catch{return false;}}
 static INPUT Input(ushort key,bool up){return new INPUT{Type=1,Data=new Union{Key=new KEYBDINPUT{Vk=key,Flags=(up?2u:0u)|((key>=0x21&&key<=0x2E)?1u:0u),Extra=new UIntPtr(0x52544B47u)}}};}
 static void Key(ushort key,bool shift){var a=shift?new INPUT[]{Input(0x10,false),Input(key,false),Input(key,true),Input(0x10,true)}:new INPUT[]{Input(key,false),Input(key,true)};uint sent=SendInput((uint)a.Length,a,Marshal.SizeOf(typeof(INPUT)));if(sent!=a.Length)throw new Exception("SendInput "+sent+"/"+a.Length+" error "+Marshal.GetLastWin32Error());}
 delegate bool EnumProc(IntPtr h,IntPtr l);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr data);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 void ClearHeld(long now){LastUsage=0;LastSource="";Repeater.Update(0,now);}
 // Compatibility entry for Consumer packets.
 void HandleUsage(string device,ushort usage,bool repeat=false){HandleInput(device,new RemoteInputEvent{InputType="Consumer",Code=usage,IsRelease=usage==0},repeat);}
 void HandleInput(string device,RemoteInputEvent input,bool repeat=false){
  if(!ConsumerInput.IsTarget(device)||input==null||(input.InputType!="Consumer"&&input.InputType!="Keyboard"))return;
  long now=Clock.ElapsedMilliseconds;string source=input.InputType;ushort code=input.Code;
  if(source=="Consumer"&&code==0x223)System.Threading.Interlocked.Exchange(ref RemoteHomeTick,now);
  if(LearnGate.IsLearning()){if(Executor!=null)Executor.Cancel();ClearHeld(now);return;}
  if(input.IsRelease){if(source==LastSource&&(source=="Consumer"||code==LastUsage))ClearHeld(now);return;}
  if(code==0)return;
  if(LastUsage==code&&LastSource==source&&!repeat)return;
  var b=Config.FindInput(source,code);
  if(b==null)return;
  if(!repeat){if(LastSource!=source)Repeater.Update(0,now);LastSource=source;LastUsage=code;HeldDevice=device;Repeater.Update(code,now,b.Repeat,b.Delay,b.Interval);}
  Func<bool> permitted=null;if(b.Scope=="Application"){string expected;try{expected=ActionValidation.ResolvePath(b.ScopeApplicationPath,ConfigPath);}catch(Exception ex){Log("SCOPE_ERROR "+ex.Message);return;}permitted=()=>ScopeActive(expected);if(!permitted())return;}if(b.Action=="Disabled"||b.Action=="Native")return;if(b.Action=="Key"||b.Action=="OpenTargetApplication"||b.Action=="LaunchApplication"||b.Action=="OpenTarget"||b.Action=="Mouse"||b.Action=="Sequence"){if(Executor==null)Executor=new ActionExecutor(ActionAdapter,Log);if(!Executor.TryExecute(b,ConfigPath,permitted)){Log("ACTION_BUSY ignored");return;}}LastAction=b.Action+":"+b.Key;Log("ACTION source="+source+" code="+code.ToString("X4")+" action="+LastAction);
 }
 IntPtr KeyboardHook(int code,IntPtr wp,IntPtr lp){if(code>=0){var k=(KeyData)Marshal.PtrToStructure(lp,typeof(KeyData));if(k.Vk==0xAC&&k.Extra!=new UIntPtr(0x52544B47u)){int msg=wp.ToInt32();if(msg==0x100||msg==0x104){lock(HomeLock)PendingHomes.Add(Clock.ElapsedMilliseconds);}return new IntPtr(1);}}return CallNextHookEx(Hook,code,wp,lp);}
 void Tick(){if(LearnGate.IsLearning()){if(Executor!=null)Executor.Cancel();ClearHeld(Clock.ElapsedMilliseconds);}ushort held=Repeater.Due(Clock.ElapsedMilliseconds);if(held!=0)HandleInput(HeldDevice,new RemoteInputEvent{InputType=LastSource,Code=held},true);string line;while(HookLogs.TryDequeue(out line))Log(line);long now=Clock.ElapsedMilliseconds;lock(HomeLock){for(int i=PendingHomes.Count-1;i>=0;i--){long t=PendingHomes[i];if(now-t>=120){PendingHomes.RemoveAt(i);if(Math.Abs(System.Threading.Interlocked.Read(ref RemoteHomeTick)-t)>180){Key(0xAC,false);}}}}}
 void State(){try{float volume;bool mute;Audio.Get(out volume,out mute);uint pid;IntPtr fw=GetForegroundWindow();GetWindowThreadProcessId(fw,out pid);string fg="";try{fg=Process.GetProcessById((int)pid).ProcessName;}catch{}var obj=new{time=DateTime.Now.ToString("O"),pid=Process.GetCurrentProcess().Id,session=Process.GetCurrentProcess().SessionId,volume=volume,mute=mute,foreground=fg,lastAction=LastAction};File.WriteAllText(Path.Combine(Dir,"state.json"),new JavaScriptSerializer().Serialize(obj));}catch(Exception e){Log("STATE_ERROR "+e.Message);}}
 public RockTekRemote(){Directory.CreateDirectory(Dir);Reload();ownedIcon=AppIcon.Create();Icon=ownedIcon;Tray=new NotifyIcon{Icon=ownedIcon,Text="RockTek Remote",Visible=true};var menu=new ContextMenuStrip();menu.Items.Add(UiText.Get("Открыть настройки"),null,(s,e)=>OpenSettings());menu.Items.Add(UiText.Get("Выход"),null,(s,e)=>Application.Exit());Tray.ContextMenuStrip=menu;Tray.DoubleClick+=(s,e)=>OpenSettings();ShowInTaskbar=false;Opacity=0;WindowState=FormWindowState.Minimized;Text="RockTek Remote";var h=Handle;ConsumerInput.Register(h,false);Callback=KeyboardHook;var ready=new System.Threading.ManualResetEvent(false);int hookError=0;HookThread=new System.Threading.Thread(()=>{Hook=SetWindowsHookEx(13,Callback,GetModuleHandle(null),0);hookError=Marshal.GetLastWin32Error();ready.Set();if(Hook!=IntPtr.Zero)Application.Run();});HookThread.IsBackground=true;HookThread.SetApartmentState(System.Threading.ApartmentState.STA);HookThread.Start();ready.WaitOne();if(Hook==IntPtr.Zero)throw new Exception("Keyboard hook "+hookError);var timer=new Timer();timer.Interval=30;timer.Tick+=(s,e)=>{try{Tick();}catch(Exception ex){Log("ERROR "+ex.Message);}};timer.Start();var state=new Timer();state.Interval=1000;state.Tick+=(s,e)=>{try{Reload();State();}catch(Exception ex){Log("ERROR "+ex.Message);}};state.Start();Log("READY session="+Process.GetCurrentProcess().SessionId);State();}
 protected override void SetVisibleCore(bool value){base.SetVisibleCore(false);}
 protected override void WndProc(ref Message m){if(m.Msg==0xFF)try{ConsumerInput.ReadEvents(m.LParam,(dev,input)=>HandleInput(dev,input));}catch(Exception e){Log("ERROR "+e.Message);}base.WndProc(ref m);}
 protected override void Dispose(bool d){if(d&&Executor!=null)Executor.Dispose();if(Hook!=IntPtr.Zero){UnhookWindowsHookEx(Hook);Hook=IntPtr.Zero;}if(d&&Tray!=null){Tray.Visible=false;Tray.Dispose();}base.Dispose(d);if(d&&ownedIcon!=null){ownedIcon.Dispose();ownedIcon=null;}}
 [STAThread] static void Main(string[] args){UiText.Initialize(args);WindowLayout.InitializeDpi();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{if(Array.IndexOf(args,"--uninstall")>=0){Installation.Uninstall(args);return;}ConfigUsesLocator=!AppPaths.IsExplicit(args);ConfigPath=AppPaths.Settings(args);if(Array.IndexOf(args,"--settings-only")>=0||Array.IndexOf(args,"--preview")>=0){Application.Run(new SettingsForm(ConfigPath,ConfigUsesLocator));return;}if(DeviceFilter.Target.Length==0)throw new ArgumentException(UiText.Get("Укажите Bluetooth-адрес пульта в ROCKTEK_REMOTE_DEVICE (12 шестнадцатеричных символов), затем перезапустите программу."));using(var mutex=new System.Threading.Mutex(false,"Local\\RockTekRemote")){if(!mutex.WaitOne(0)){Application.Run(new SettingsForm(ConfigPath,ConfigUsesLocator));return;}try{Application.Run(new RockTekRemote());}finally{mutex.ReleaseMutex();}}}catch(Exception e){Directory.CreateDirectory(Dir);Log("FATAL "+e);MessageBox.Show(e.Message,"RockTek Remote");Environment.ExitCode=1;}}
}
[ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]class Enumerator{}
[ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IEnum{[PreserveSig]int EnumAudioEndpoints(int f,int s,out IntPtr d);[PreserveSig]int GetDefaultAudioEndpoint(int f,int r,out IDevice d);[PreserveSig]int GetDevice(string id,out IDevice d);[PreserveSig]int RegisterEndpointNotificationCallback(IntPtr p);[PreserveSig]int UnregisterEndpointNotificationCallback(IntPtr p);}
[ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IDevice{[PreserveSig]int Activate(ref Guid iid,uint ctx,IntPtr p,[MarshalAs(UnmanagedType.IUnknown)]out object o);[PreserveSig]int OpenPropertyStore(int a,out IntPtr p);[PreserveSig]int GetId(out IntPtr p);[PreserveSig]int GetState(out int s);}
[ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IVolume{[PreserveSig]int RegisterControlChangeNotify(IntPtr p);[PreserveSig]int UnregisterControlChangeNotify(IntPtr p);[PreserveSig]int GetChannelCount(out uint n);[PreserveSig]int SetMasterVolumeLevel(float f,ref Guid g);[PreserveSig]int SetMasterVolumeLevelScalar(float f,ref Guid g);[PreserveSig]int GetMasterVolumeLevel(out float f);[PreserveSig]int GetMasterVolumeLevelScalar(out float f);[PreserveSig]int SetChannelVolumeLevel(uint c,float f,ref Guid g);[PreserveSig]int SetChannelVolumeLevelScalar(uint c,float f,ref Guid g);[PreserveSig]int GetChannelVolumeLevel(uint c,out float f);[PreserveSig]int GetChannelVolumeLevelScalar(uint c,out float f);[PreserveSig]int SetMute([MarshalAs(UnmanagedType.Bool)]bool m,ref Guid g);[PreserveSig]int GetMute([MarshalAs(UnmanagedType.Bool)]out bool m);}
static class Audio{public static void Get(out float volume,out bool mute){IEnum e=(IEnum)new Enumerator();IDevice d=null;object o=null;try{Marshal.ThrowExceptionForHR(e.GetDefaultAudioEndpoint(0,1,out d));Guid id=typeof(IVolume).GUID;Marshal.ThrowExceptionForHR(d.Activate(ref id,23,IntPtr.Zero,out o));var v=(IVolume)o;Marshal.ThrowExceptionForHR(v.GetMasterVolumeLevelScalar(out volume));Marshal.ThrowExceptionForHR(v.GetMute(out mute));}finally{if(o!=null)Marshal.ReleaseComObject(o);if(d!=null)Marshal.ReleaseComObject(d);Marshal.ReleaseComObject(e);}}}
