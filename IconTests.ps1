param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @'
using System;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Text;
public static class IconResources {
 delegate bool EnumName(IntPtr module,IntPtr type,IntPtr name,IntPtr value);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
 [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr h);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool EnumResourceNames(IntPtr h,IntPtr type,EnumName callback,IntPtr value);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindResource(IntPtr h,IntPtr name,IntPtr type);
 [DllImport("kernel32.dll")]static extern IntPtr LoadResource(IntPtr h,IntPtr info);
 [DllImport("kernel32.dll")]static extern IntPtr LockResource(IntPtr resource);
 [DllImport("kernel32.dll")]static extern uint SizeofResource(IntPtr h,IntPtr info);
 static byte[] Read(IntPtr h,IntPtr name,int type){IntPtr info=FindResource(h,name,new IntPtr(type));if(info==IntPtr.Zero)throw new Exception("Missing native resource "+type);IntPtr data=LockResource(LoadResource(h,info));byte[] bytes=new byte[SizeofResource(h,info)];Marshal.Copy(data,bytes,0,bytes.Length);return bytes;}
 public static string Verify(string path){IntPtr h=LoadLibraryEx(path,IntPtr.Zero,2);if(h==IntPtr.Zero)throw new Exception("Cannot load PE resources");try{var groups=new List<IntPtr>();var icons=new List<IntPtr>();EnumName group=(m,t,n,v)=>{groups.Add(n);return true;};EnumName image=(m,t,n,v)=>{icons.Add(n);return true;};EnumResourceNames(h,new IntPtr(14),group,IntPtr.Zero);EnumResourceNames(h,new IntPtr(3),image,IntPtr.Zero);if(groups.Count<1||icons.Count<7)throw new Exception("PE icon resources missing multi-resolution images");byte[] g=Read(h,groups[0],14);if(BitConverter.ToUInt16(g,4)!=7)throw new Exception("Expected seven icon sizes");string manifest=Encoding.UTF8.GetString(Read(h,new IntPtr(1),24));if(!manifest.Contains("asInvoker"))throw new Exception("Expected asInvoker PE manifest");return "7 multi-resolution PE icons and asInvoker manifest";}finally{FreeLibrary(h);}}
}
'@
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$sha=[Security.Cryptography.SHA256]::Create()
try{
foreach($exe in @($Assembly,(Join-Path (Split-Path $Assembly) 'RockTekRemoteSetup.exe'))){
$result=[IconResources]::Verify($exe);Assert ($result -like '7*') ('native resources '+(Split-Path $exe -Leaf))
$a=[Reflection.Assembly]::LoadFrom($exe);$stream=$a.GetManifestResourceStream('app-icon.ico');try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','');Assert ($hash -eq (Get-FileHash "$PSScriptRoot\assets\app-icon.ico").Hash) ('managed icon bytes exactly match asset '+(Split-Path $exe -Leaf))}finally{$stream.Dispose()}
[UiText]::Initialize([string[]]@("--language","ru"))
}
function BitmapHash($icon){$b=$icon.ToBitmap();$s=[IO.MemoryStream]::new();try{$b.Save($s,[Drawing.Imaging.ImageFormat]::Png);[BitConverter]::ToString($sha.ComputeHash($s.ToArray()))}finally{$s.Dispose();$b.Dispose()}}
$expected=[AppIcon]::Create();$p=Join-Path $PSScriptRoot 'icon-settings.json';[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save($p)
try{$f=[SettingsForm]::new($p);try{Assert ((BitmapHash $f.Icon) -eq (BitmapHash $expected)) 'actual SettingsForm owns custom app icon';$seq=[SequenceEditorForm]::new($null);try{Assert ((BitmapHash $seq.Icon) -eq (BitmapHash $expected)) 'sequence editor owns custom app icon'}finally{$seq.Dispose()}}finally{$f.Dispose()}}finally{$expected.Dispose();Remove-Item $p -ErrorAction SilentlyContinue}
}finally{$sha.Dispose()}
'ALL ICON TESTS PASSED'
