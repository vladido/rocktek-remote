using System;using System.Diagnostics;using System.Reflection;using System.Threading;using System.Windows.Forms;
public static class PlayerCloseTests {
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static Process Start(string mode){return Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,mode){UseShellExecute=false,CreateNoWindow=true});}
 static void Ready(Process p){var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<10000){p.Refresh();if(p.HasExited)throw new Exception("fixture exited before window");if(p.MainWindowHandle!=IntPtr.Zero)return;Thread.Sleep(50);}throw new Exception("fixture window missing");}
 [STAThread]public static int Main(string[] args){
  if(args.Length==1&&args[0]=="--window"){Application.Run(new Form{Text="Task-owned close fixture",Width=200,Height=100});return 0;}
  if(args.Length==1&&args[0]=="--headless"){Thread.Sleep(30000);return 0;}
  try{
   Check(PlayerClose.Run(null)=="ALREADY_CLOSED","closed target is a no-op");
   if(Process.GetCurrentProcess().SessionId!=0)using(var selected=Start("--window"))using(var unrelated=Start("--window")){
    try{Ready(selected);Ready(unrelated);Check(PlayerClose.Run(selected)=="CLOSED pid="+selected.Id,"native WM_CLOSE completes selected process");Check(selected.HasExited&&!unrelated.HasExited,"unselected process survives");Check(PlayerClose.Run(selected)=="ALREADY_CLOSED","repeated close does not launch or kill");PlayerClose.Run(unrelated);}
    finally{if(!selected.HasExited)selected.Kill();if(!unrelated.HasExited)unrelated.Kill();}
   }
   using(var headless=Start("--headless")){try{Thread.Sleep(200);bool refused=false;try{PlayerClose.Run(headless);}catch(InvalidOperationException){refused=true;}Check(refused&&!headless.HasExited,"no window refuses without force kill");}finally{if(!headless.HasExited)headless.Kill();}}
   Console.WriteLine(Process.GetCurrentProcess().SessionId==0?"ALL PLAYER CLOSE GUARDS PASSED; interactive window fixtures require separate interactive run":"ALL PLAYER CLOSE TESTS PASSED");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}
 }
}
