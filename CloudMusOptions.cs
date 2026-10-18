using System;using System.IO;using System.Text;using System.Text.RegularExpressions;
public static class CloudMusOptions {
 public static string CloseOnExit(string ini){
  string nl=ini.Contains("\r\n")?"\r\n":"\n";
  var section=Regex.Match(ini,@"(?ms)^\[window\]\r?\n(.*?)(?=^\[|\z)");
  if(!section.Success)return ini+(ini.Length>0&&!ini.EndsWith("\n")?nl:"")+"[window]"+nl+"closeMinimizesToTray=false"+nl;
  string body=section.Groups[1].Value;var key=new Regex(@"(?m)^closeMinimizesToTray=[^\r\n]*");
  string updated=key.IsMatch(body)?key.Replace(body,"closeMinimizesToTray=false"):"closeMinimizesToTray=false"+nl+body;
  return ini.Substring(0,section.Groups[1].Index)+updated+ini.Substring(section.Groups[1].Index+body.Length);
 }
 public static void Prepare(){
  string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"cloudmus","fronts","qt","config.ini");
  FileSafety.CheckPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path));string ini=File.Exists(path)?File.ReadAllText(path):"";string next=CloseOnExit(ini);if(next==ini)return;
  string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] b=new UTF8Encoding(false).GetBytes(next);f.Write(b,0,b.Length);f.Flush(true);}FileSafety.CheckPath(path);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
