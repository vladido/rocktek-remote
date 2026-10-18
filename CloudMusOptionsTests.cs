using System;
public static class CloudMusOptionsTests {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static int Main(){
  string before="[playback]\r\nvolume=80\r\n[window]\r\ncloseMinimizesToTray=true\r\nwidth=600\r\n[other]\r\nvalue=stay\r\n";
  string after=CloudMusOptions.CloseOnExit(before);
  Check(after==before.Replace("closeMinimizesToTray=true","closeMinimizesToTray=false"),"change only close option in window section");
  Check(CloudMusOptions.CloseOnExit(after)==after,"idempotent");
  Check(CloudMusOptions.CloseOnExit("[window]\nwidth=1\n")=="[window]\ncloseMinimizesToTray=false\nwidth=1\n","insert missing key");
  Check(CloudMusOptions.CloseOnExit("[other]\na=1\n")=="[other]\na=1\n[window]\ncloseMinimizesToTray=false\n","insert missing window section");
  Console.WriteLine("PASS CloudMus close-on-exit option preserves other settings");return 0;
 }
}
