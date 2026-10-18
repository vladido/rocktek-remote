using System;using System.IO;using System.Reflection;
public sealed class ShutdownRecordingAdapter:IActionAdapter {
 public string Image,Arguments;public int Calls;
 public bool Activate(string p){return false;}
 public void Launch(string p,string a,string d){Image=p;Arguments=a;Calls++;}
 public void Open(string p){throw new Exception("Unexpected Open");}
 public void Key(string p){throw new Exception("Unexpected key");}
 public void Mouse(string p,int n){throw new Exception("Unexpected mouse");}
}
public static class ShutdownDispatchTests {
 public static int Main(){try{
  string dist=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
  foreach(string player in new[]{"CloudMus","Murglar"}){
   string file=Path.Combine(dist,"profiles",player+".json");var config=RemoteConfig.Load(file);var adapter=new ShutdownRecordingAdapter();
   using(var executor=new ActionExecutor(adapter,null)){
    if(!executor.TryExecute(config.Find(48),file)||!executor.WaitForIdle(5000))throw new Exception("Power dispatch failed");
    if(adapter.Calls!=1||!String.Equals(adapter.Image,Path.Combine(Environment.GetEnvironmentVariable("WINDIR"),"System32","shutdown.exe"),StringComparison.OrdinalIgnoreCase)||adapter.Arguments!="/s /t 0")throw new Exception("Wrong shutdown command");
    Console.WriteLine("PASS "+player+" shutdown dispatch recorded; computer was NOT shut down");
   }
  }Console.WriteLine("ALL SHUTDOWN DISPATCH TESTS PASSED");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
