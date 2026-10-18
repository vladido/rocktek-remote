using System;using System.IO;using System.Reflection;
// Fixed installer payload allowlist. Editable profiles are never overwritten or removed.
public static class PlayerPayloads {
 public static readonly string[] Helpers={"CloudMusPlayPause.exe","MurglarRemote.exe"};
 public static void Check(string root,bool owned){
  foreach(string name in Helpers){string file=Path.Combine(root,name);FileSafety.CheckPath(file);if(File.Exists(file)&&!owned)throw new IOException("Unowned existing player helper; installation refused.");}
  foreach(string name in new[]{"CloudMus","Murglar"})FileSafety.CheckPath(Path.Combine(root,"profiles",name+".json"));
 }
 static void Extract(string resource,string path,bool createOnly){
  FileSafety.CheckPath(path);if(createOnly&&File.Exists(path))return;Directory.CreateDirectory(Path.GetDirectoryName(path));
  string temporary=path+"."+Guid.NewGuid().ToString("N")+".new";
  try{using(Stream source=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)){if(source==null)throw new IOException("Missing player payload: "+resource);using(var target=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){source.CopyTo(target);target.Flush(true);}}
   FileSafety.CheckPath(path);if(!createOnly&&File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 public static void Install(string root){foreach(string name in Helpers)Extract(name,Path.Combine(root,name),false);foreach(string name in new[]{"CloudMus","Murglar"})Extract("profiles."+name+".json",Path.Combine(root,"profiles",name+".json"),true);}
 public static void Remove(string root){foreach(string name in Helpers){string file=Path.Combine(root,name);FileSafety.CheckPath(file);if(File.Exists(file))File.Delete(file);}}
}
