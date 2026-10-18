using System;using System.IO;using System.Text;using System.Threading;using System.Diagnostics;using System.Runtime.InteropServices;using System.Collections.Generic;using System.Windows.Forms;
public static class MurglarPolicy {
 public static bool RatingMatch(string mode,bool menu,string role,string name){
  if(mode=="--like")return !menu&&role=="push button"&&Array.IndexOf(new[]{"Добавить себе","Добавить в свои","Удалить из своих","Like","Dislike"},name)>=0;
  if(mode!="--dislike")return false;
  if(menu)return role=="label"&&Array.IndexOf(new[]{"Не рекомендовать трек","Don't recommend track"},name)>=0;
  return role=="push button"&&Array.IndexOf(new[]{"Don't recommend","Не рекомендовать"},name)>=0;
 }
 // A loaded transport always takes precedence over the initial station choice.
 public static string Decide(int pause,int play,int wave,int yandex){if(pause==1&&play==0)return "PAUSE";if(play==1&&pause==0)return "PLAY";if(pause!=0||play!=0)return "ERROR";if(wave==1)return "WAVE";if(wave==0&&yandex==1)return "YANDEX";return "ERROR";}
}
public static class MurglarRemote {
 static string Expected=PlayerTarget.DefaultMurglar;
 static string Dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RockTekRemote");
 static string State=Path.Combine(Dir,"murglar-playpause-state.txt");
 static int Result(int code,string text){File.WriteAllText(State,DateTime.UtcNow.ToString("o")+" "+text,Encoding.UTF8);return code;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct Info {
 [MarshalAs(UnmanagedType.ByValTStr,SizeConst=1024)]public string name;
 [MarshalAs(UnmanagedType.ByValTStr,SizeConst=1024)]public string description;
 [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string role;
 [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string roleEN;
 [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string states;
 [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string statesEN;
 public int index,children,x,y,w,h,component,action,selection,text,interfaces;
 }
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool SetDllDirectory(string path);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 delegate bool EnumProc(IntPtr h,IntPtr arg);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc cb,IntPtr p);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("windowsaccessbridge-64.dll")]static extern void Windows_run();
 [DllImport("windowsaccessbridge-64.dll")]static extern bool isJavaWindow(IntPtr h);
 [DllImport("windowsaccessbridge-64.dll")]static extern bool getAccessibleContextFromHWND(IntPtr h,out int vm,out long ac);
 [DllImport("windowsaccessbridge-64.dll")]static extern bool getAccessibleContextInfo(int vm,long ac,out Info info);
 [DllImport("windowsaccessbridge-64.dll")]static extern long getAccessibleChildFromContext(int vm,long ac,int index);
 [DllImport("windowsaccessbridge-64.dll")]static extern void releaseJavaObject(int vm,long ac);
 [DllImport("windowsaccessbridge-64.dll")]static extern bool getAccessibleActions(int vm,long ac,IntPtr actions);
 [DllImport("windowsaccessbridge-64.dll")]static extern bool doAccessibleActions(int vm,long ac,IntPtr actions,out int failure);
 class Node{public long ac;public Info info;}
 class Tree:IDisposable {
  public int vm;public List<Node> nodes=new List<Node>();Stopwatch timer=Stopwatch.StartNew();
  public Tree(IntPtr window){long root;if(!getAccessibleContextFromHWND(window,out vm,out root))throw new InvalidOperationException("java-context-unavailable");try{Walk(root,0);}catch{Dispose();throw;}}
  void Walk(long ac,int depth){if(ac==0)return;var n=new Node{ac=ac};nodes.Add(n);if(nodes.Count>800||depth>24||timer.ElapsedMilliseconds>2500)throw new InvalidOperationException("tree-bound");if(!getAccessibleContextInfo(vm,ac,out n.info))throw new InvalidOperationException("tree-read");if(n.info.children<0||n.info.children>500)throw new InvalidOperationException("child-bound");for(int i=0;i<n.info.children;i++)Walk(getAccessibleChildFromContext(vm,ac,i),depth+1);}
  public List<Node> Buttons(params string[] names){return nodes.FindAll(n=>n.info.roleEN=="push button"&&n.info.action!=0&&n.info.statesEN!=null&&Array.IndexOf(n.info.statesEN.Split(','),"enabled")>=0&&Array.IndexOf(names,n.info.name)>=0);}
  public List<Node> Ratings(string mode,bool menu){return nodes.FindAll(n=>n.info.action!=0&&n.info.statesEN!=null&&Array.IndexOf(n.info.statesEN.Split(','),"enabled")>=0&&MurglarPolicy.RatingMatch(mode,menu,n.info.roleEN,n.info.name));}
  public string LikeState(){var buttons=Ratings("--like",false);if(buttons.Count!=1)return "unavailable";return Array.IndexOf(new[]{"Удалить из своих","Dislike"},buttons[0].info.name)>=0?"liked":"unliked";}
  public string Timers(){var values=new List<string>();foreach(var n in nodes)if(n.info.roleEN=="label"&&System.Text.RegularExpressions.Regex.IsMatch(n.info.name??"","^\\d+:\\d\\d$"))values.Add(n.info.name);return String.Join("/",values.ToArray());}
  public void Dispose(){foreach(var n in nodes)releaseJavaObject(vm,n.ac);nodes.Clear();}
 }
 static bool Foreground(int pid){uint p;GetWindowThreadProcessId(GetForegroundWindow(),out p);return p==pid;}
 static bool VisibleWindow(int pid){bool found=false;EnumWindows((h,l)=>{uint p;GetWindowThreadProcessId(h,out p);if(p==pid&&IsWindowVisible(h)){found=true;return false;}return true;},IntPtr.Zero);return found;}
 static Process Target(){
  var all=new List<Process>();Process selected=null;
  try{
   foreach(var p in Process.GetProcessesByName("Murglar")){bool valid=false;try{valid=p.SessionId==Process.GetCurrentProcess().SessionId&&String.Equals(p.MainModule.FileName,Expected,StringComparison.OrdinalIgnoreCase);}catch{}if(valid)all.Add(p);else p.Dispose();}
   if(all.Count==0)return null;
   var ids=new HashSet<int>();foreach(var p in all)ids.Add(p.Id);
   var parents=new Dictionary<int,int>();using(var search=new System.Management.ManagementObjectSearcher("SELECT ProcessId,ParentProcessId FROM Win32_Process WHERE Name='Murglar.exe'"))using(var rows=search.Get())foreach(System.Management.ManagementObject row in rows)using(row){int id=Convert.ToInt32(row["ProcessId"]);if(ids.Contains(id))parents[id]=Convert.ToInt32(row["ParentProcessId"]);}
   var roots=new HashSet<int>();foreach(int id in ids){int root=id,parent,steps=0;while(parents.TryGetValue(root,out parent)&&ids.Contains(parent)){root=parent;if(++steps>16)throw new InvalidOperationException("process-parent-cycle");}roots.Add(root);}
   if(roots.Count!=1)throw new InvalidOperationException("multiple-murglar-instances");
   foreach(var p in all)if(VisibleWindow(p.Id)){if(selected!=null)throw new InvalidOperationException("multiple-murglar-windows");selected=p;}
   if(selected==null){foreach(var p in all)if(roots.Contains(p.Id)){selected=p;break;}}
   return selected;
  }catch{selected=null;throw;}finally{foreach(var p in all)if(p!=selected)p.Dispose();}
 }
 static IntPtr Window(int pid){IntPtr found=IntPtr.Zero;EnumWindows((h,l)=>{uint p;GetWindowThreadProcessId(h,out p);if(p==pid&&IsWindowVisible(h)&&isJavaWindow(h)){found=h;return false;}return true;},IntPtr.Zero);return found;}
 static void Pump(int ms){var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(20);}}
 static void Invoke(Tree tree,Node node,int pid){
  if(!Foreground(pid))throw new InvalidOperationException("foreground-changed");
  IntPtr actions=Marshal.AllocHGlobal(4+256*512),todo=Marshal.AllocHGlobal(4+32*512);
  try{if(!getAccessibleActions(tree.vm,node.ac,actions)||Marshal.ReadInt32(actions)!=1)throw new InvalidOperationException("ambiguous-action");string name=Marshal.PtrToStringUni(IntPtr.Add(actions,4),256).TrimEnd('\0');if(name.Length==0||name.Length>255)throw new InvalidOperationException("bad-action");byte[] data=new byte[4+32*512];Buffer.BlockCopy(BitConverter.GetBytes(1),0,data,0,4);byte[] text=Encoding.Unicode.GetBytes(name+"\0");Buffer.BlockCopy(text,0,data,4,text.Length);Marshal.Copy(data,0,todo,data.Length);int failure;if(!Foreground(pid)||!doAccessibleActions(tree.vm,node.ac,todo,out failure))throw new InvalidOperationException("invoke-failed");}finally{Marshal.FreeHGlobal(actions);Marshal.FreeHGlobal(todo);}
 }
 static int Open(){
  Process p=Target();if(p==null){var s=new ProcessStartInfo(Expected){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(Expected)};s.EnvironmentVariables["JAVA_TOOL_OPTIONS"]=(s.EnvironmentVariables["JAVA_TOOL_OPTIONS"]??"")+" -Djavax.accessibility.assistive_technologies=com.sun.java.accessibility.AccessBridge";p=Process.Start(s);}
  using(p){var assembly=System.Reflection.Assembly.LoadFrom(Path.Combine(PlayerTarget.AssemblyDirectory,"RockTekRemote.exe"));var adapter=Activator.CreateInstance(assembly.GetType("WindowsActionAdapter",true));var activate=adapter.GetType().GetMethod("Activate");var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<15000){Pump(100);using(var active=Target()){if(active==null)return Result(8,"ERROR murglar-exited");IntPtr h=Window(active.Id);if(h==IntPtr.Zero)continue;using(var tree=new Tree(h)){if(tree.Buttons("Settings").Count!=1)continue;}try{if((bool)activate.Invoke(adapter,new object[]{Expected})&&Foreground(active.Id))return Result(0,"ACTIVATED pid="+active.Id);}catch(System.Reflection.TargetInvocationException){}}}return Result(9,"ERROR readiness-or-foreground-timeout");}
 }
 static int Play(bool status){using(var p=Target()){
  if(p==null)return Result(3,"SKIP murglar-closed");if(!status&&!Foreground(p.Id))return Result(3,"SKIP foreground-not-murglar");
  var timer=Stopwatch.StartNew();bool selectedService=false;
  while(timer.ElapsedMilliseconds<8000){Pump(100);IntPtr h=Window(p.Id);if(h==IntPtr.Zero)continue;using(var tree=new Tree(h)){
   var pause=tree.Buttons("Пауза","Pause");var play=tree.Buttons("Воспроизвести","Play");var wave=tree.Buttons("Моя волна");var yandex=tree.Buttons("Яндекс");
   if(status)return Result(0,"STATUS pid="+p.Id+" pause="+pause.Count+" play="+play.Count+" wave="+wave.Count+" like="+tree.LikeState()+" dislikeButton="+tree.Ratings("--dislike",false).Count+" dislikeTrack="+tree.Ratings("--dislike",true).Count+" timers="+tree.Timers());
   string action=MurglarPolicy.Decide(pause.Count,play.Count,wave.Count,yandex.Count);
   if(action=="PAUSE"){Invoke(tree,pause[0],p.Id);return Result(0,"INVOKED pause pid="+p.Id);}
   if(action=="PLAY"){Invoke(tree,play[0],p.Id);return Result(0,"INVOKED resume pid="+p.Id);}
   if(action=="WAVE"){Invoke(tree,wave[0],p.Id);return Result(0,"INVOKED yandex-my-wave pid="+p.Id);}
   if(action=="YANDEX"&&!selectedService){Invoke(tree,yandex[0],p.Id);selectedService=true;Pump(300);continue;}
   return Result(4,"ERROR ambiguous-or-missing-controls");
  }}return Result(5,"ERROR java-accessibility-timeout");
 }}
 static int Rate(string mode){using(var p=Target()){
  if(p==null)return Result(3,"SKIP murglar-closed");if(!Foreground(p.Id))return Result(3,"SKIP foreground-not-murglar");bool opened=false;var timer=Stopwatch.StartNew();
  while(timer.ElapsedMilliseconds<8000){Pump(100);if(!Foreground(p.Id))return Result(3,"SKIP foreground-changed");IntPtr h=Window(p.Id);if(h==IntPtr.Zero)continue;using(var tree=new Tree(h)){
   if(mode=="--like"){var buttons=tree.Ratings(mode,false);if(buttons.Count!=1)return Result(4,"ERROR heart-control-count="+buttons.Count);string before=tree.LikeState();Invoke(tree,buttons[0],p.Id);return Result(0,"INVOKED heart-toggle before="+before+" pid="+p.Id);}
   var track=tree.Ratings(mode,true);if(track.Count>1)return Result(4,"ERROR track-menu-ambiguous");if(track.Count==1){Invoke(tree,track[0],p.Id);return Result(0,"INVOKED dislike-track pid="+p.Id);}
   if(!opened){var buttons=tree.Ratings(mode,false);if(buttons.Count!=1)return Result(4,"ERROR dislike-control-count="+buttons.Count);Invoke(tree,buttons[0],p.Id);opened=true;}
  }}return Result(5,"ERROR track-menu-timeout");
 }}
 [STAThread]public static int Main(string[] args){Directory.CreateDirectory(Dir);try{Expected=PlayerTarget.Parse(ref args,Expected);}catch(ArgumentException){return Result(10,"ERROR invalid-application-argument");}bool open=args.Length==1&&args[0]=="--open",status=args.Length==1&&args[0]=="--status";string rating=args.Length==1&&(args[0]=="--like"||args[0]=="--dislike")?args[0]:null;if(args.Length!=0&&!open&&!status&&rating==null)return 10;if(open)State=Path.Combine(Dir,"murglar-open-state.txt");if(status)State=Path.Combine(Dir,"murglar-probe-state.txt");if(rating!=null)State=Path.Combine(Dir,rating=="--like"?"murglar-like-state.txt":"murglar-dislike-state.txt");using(var gate=new Mutex(false,open?"Local\\MurglarOpen-v1":"Local\\MurglarPlayPause-v1")){if(!gate.WaitOne(0))return 2;try{if(!SetDllDirectory(Path.Combine(Path.GetDirectoryName(Expected),"runtime","bin")))throw new InvalidOperationException("bridge-dll-directory");Windows_run();return open?Open():rating!=null?Rate(rating):Play(status);}catch(Exception e){return Result(1,"ERROR "+e.GetType().Name+" "+e.Message);}finally{gate.ReleaseMutex();}}}
}
