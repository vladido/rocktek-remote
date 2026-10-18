using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
public static class CloudMusPlayPause {
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint id);
 static string State=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RockTekRemote","cloudmus-playpause-state.txt");
 static int Result(int code,string value){File.WriteAllText(State,DateTime.UtcNow.ToString("o")+" "+value);return code;}
 static string Expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","CloudMus","cloudmus-qt.exe");
 static int Open(){
  Process target=null;
  foreach(var p in Process.GetProcessesByName("cloudmus-qt")){
   try{if(p.SessionId==Process.GetCurrentProcess().SessionId&&String.Equals(p.MainModule.FileName,Expected,StringComparison.OrdinalIgnoreCase)){
    if(target!=null){p.Dispose();target.Dispose();return Result(7,"ERROR multiple-cloudmus-processes");}target=p;continue;
   }}catch{}p.Dispose();
  }
  if(target==null){CloudMusOptions.Prepare();target=Process.Start(new ProcessStartInfo(Expected){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(Expected)});}
  using(target){
   var assembly=System.Reflection.Assembly.LoadFrom(Path.Combine(PlayerTarget.AssemblyDirectory,"RockTekRemote.exe"));
   var type=assembly.GetType("WindowsActionAdapter",true);var adapter=Activator.CreateInstance(type);var activate=type.GetMethod("Activate");
   var timer=Stopwatch.StartNew();
   while(timer.ElapsedMilliseconds<15000){
    if(target.HasExited)return Result(8,"ERROR cloudmus-exited");
    var c=new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty,target.Id),new PropertyCondition(AutomationElement.AutomationIdProperty,"QApplication.Ui::MainWindow.transportToolBar.Ui::NowPlayingBar.playPauseButton"));
    if(AutomationElement.RootElement.FindFirst(TreeScope.Descendants,c)!=null){
     try{if((bool)activate.Invoke(adapter,new object[]{Expected})){
      uint fg;GetWindowThreadProcessId(GetForegroundWindow(),out fg);
      if(fg==target.Id)return Result(0,"ACTIVATED pid="+target.Id);
     }}catch(System.Reflection.TargetInvocationException){}
    }
    Thread.Sleep(150);
   }
   return Result(9,"ERROR foreground-timeout pid="+target.Id);
  }
 }
 [STAThread] public static int Main(string[] args){
  Directory.CreateDirectory(Path.GetDirectoryName(State));
  try{Expected=PlayerTarget.Parse(ref args,Expected);}catch(ArgumentException){return Result(10,"ERROR invalid-application-argument");}
  bool opening=args.Length==1&&args[0]=="--open";
  if(args.Length!=0&&!opening)return 10;
  if(opening)State=Path.Combine(Path.GetDirectoryName(State),"cloudmus-open-state.txt");
  using(var gate=new Mutex(false,opening?"Local\\CloudMusOpen-v1":"Local\\CloudMusPlayPause-v1")){
   if(!gate.WaitOne(0))return 2;
   try{
    if(opening)return Open();
    uint id;GetWindowThreadProcessId(GetForegroundWindow(),out id);
    using(var p=Process.GetProcessById((int)id)){
     string expected=Expected;
     if(p.SessionId!=Process.GetCurrentProcess().SessionId||!String.Equals(p.MainModule.FileName,expected,StringComparison.OrdinalIgnoreCase))return Result(3,"SKIP foreground-not-cloudmus");
     var byProcess=new PropertyCondition(AutomationElement.ProcessIdProperty,p.Id);
     var byId=new PropertyCondition(AutomationElement.AutomationIdProperty,"QApplication.Ui::MainWindow.transportToolBar.Ui::NowPlayingBar.playPauseButton");
     var buttons=AutomationElement.RootElement.FindAll(TreeScope.Descendants,new AndCondition(byProcess,byId));
     if(buttons.Count!=1)return Result(4,"ERROR play-button-count="+buttons.Count);
     var button=buttons[0];
     if(!button.Current.IsEnabled)return Result(5,"SKIP play-button-disabled");
     uint current;GetWindowThreadProcessId(GetForegroundWindow(),out current);if(current!=id)return Result(3,"SKIP foreground-changed");
     object pattern;if(!button.TryGetCurrentPattern(InvokePattern.Pattern,out pattern))return Result(6,"ERROR no-invoke-pattern");
     ((InvokePattern)pattern).Invoke();return Result(0,"INVOKED pid="+id);
    }
   }catch(Exception e){return Result(1,"ERROR "+e.GetType().Name);}finally{gate.ReleaseMutex();}
  }
 }
}