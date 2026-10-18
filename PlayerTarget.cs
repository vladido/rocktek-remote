using System;using System.IO;
public static class PlayerTarget {
 public static string AssemblyDirectory {get{return Path.GetDirectoryName(typeof(PlayerTarget).Assembly.Location);}}
 public static string DefaultMurglar {get{string root=Environment.GetEnvironmentVariable("OneDrive");if(String.IsNullOrWhiteSpace(root))root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"OneDrive");return Path.Combine(root,"Murglar-Desktop-windows-x64","Murglar.exe");}}
 public static string Parse(ref string[] args,string defaultPath){
  string target=defaultPath;bool seen=false;var keep=new System.Collections.Generic.List<string>();
  for(int i=0;i<args.Length;i++){if(args[i]!="--application"){keep.Add(args[i]);continue;}if(seen||++i>=args.Length)throw new ArgumentException("invalid-application-argument");seen=true;target=Environment.ExpandEnvironmentVariables(args[i]);}
  if(String.IsNullOrWhiteSpace(target)||!Path.IsPathRooted(target)||target.IndexOf('"')>=0||!String.Equals(Path.GetExtension(target),".exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("absolute-application-exe-required");
  args=keep.ToArray();return Path.GetFullPath(target);
 }
}
