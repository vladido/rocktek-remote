using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
// Only a locator lives locally. All player/button settings stay in the selected JSON.
public static class ConfigLocation {
 public static string DefaultFile {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RockTekRemote","config-location.json");}}
 public static string CheckSettingsPath(string path){
  if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path))throw new IOException(UiText.Get("Укажите полный путь к JSON настроек."));
  string full=Path.GetFullPath(path);if(!String.Equals(Path.GetExtension(full),".json",StringComparison.OrdinalIgnoreCase)||Directory.Exists(full))throw new IOException(UiText.Get("Выберите файл JSON, не папку."));
  // Refuse NTFS alternate data streams and device namespace paths.
  if(full.StartsWith("\\\\?\\",StringComparison.Ordinal)||full.StartsWith("\\\\.\\",StringComparison.Ordinal)||full.IndexOf(':',2)>=0)throw new IOException(UiText.Get("Недопустимый путь JSON."));
  FileSafety.CheckPath(full);return full;
 }
 public static string Read(string locator){
  FileSafety.CheckPath(locator);if(!File.Exists(locator))return null;
  if(new FileInfo(locator).Length>65536)throw new IOException(UiText.Get("Слишком большой указатель настроек."));
  var raw=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(locator));
  if(raw==null||raw.Count!=2||!raw.ContainsKey("Version")||!raw.ContainsKey("SettingsPath")||!(raw["SettingsPath"] is string)||!(raw["Version"] is int)||(int)raw["Version"]!=1)throw new IOException(UiText.Get("Некорректный указатель настроек."));
  string target=CheckSettingsPath((string)raw["SettingsPath"]);
  if(!File.Exists(target))throw new IOException(UiText.Get("Выбранный файл настроек отсутствует: ")+AppPaths.Display(target));
  RemoteConfig.Load(target);return target;
 }
 public static void CheckDistinct(string locator,string target){FileSafety.CheckPath(locator);if(String.Equals(Path.GetFullPath(locator),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))throw new IOException(UiText.Get("Указатель и файл настроек должны быть разными файлами."));}
 public static void Commit(string locator,string target){
  CheckDistinct(locator,target);
  target=CheckSettingsPath(target);RemoteConfig.Load(target);FileSafety.CheckPath(locator);
  string json=new JavaScriptSerializer().Serialize(new Dictionary<string,object>{{"Version",1},{"SettingsPath",target}});
  AtomicWrite(locator,json,false);
 }
 // Create without replacement, even if another process creates the destination meanwhile.
 public static void CreateSettings(string path,RemoteConfig config){path=CheckSettingsPath(path);AtomicWrite(path,config.Json(),true);}
 static void AtomicWrite(string path,string json,bool createOnly){
  path=Path.GetFullPath(path);FileSafety.CheckPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path));FileSafety.CheckPath(path);
  string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{using(var fs=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] b=new System.Text.UTF8Encoding(false).GetBytes(json);fs.Write(b,0,b.Length);fs.Flush(true);}
   FileSafety.CheckPath(path);if(!createOnly&&File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
