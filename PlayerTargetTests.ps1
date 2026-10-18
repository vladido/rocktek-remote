param([string]$Stage=$PSScriptRoot)
$ErrorActionPreference='Stop'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source=@'
using System;using System.IO;
public static class PlayerTargetTests {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static int Main(){
  var args=new[]{"--open","--application",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Test Player","Murglar.exe")};
  string target=PlayerTarget.Parse(ref args,"unused.exe");
  Check(args.Length==1&&args[0]=="--open","retain operation after target extraction");
  Check(Path.IsPathRooted(target)&&target.EndsWith("Test Player\\Murglar.exe"),"exact path with spaces");
  args=new[]{"--like"};Check(PlayerTarget.Parse(ref args,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Murglar.exe")).EndsWith("Murglar.exe"),"legacy default");
  foreach(var bad in new[]{new[]{"--application"},new[]{"--application","relative.exe"},new[]{"--application","C:\\bad.txt"},new[]{"--application",@"\\server\share\player.exe"},new[]{"--application",@"C:\safe.exe:other.exe"},new[]{"--application","C:\\a.exe","--application","C:\\b.exe"}}){bool rejected=false;try{PlayerTarget.Parse(ref bad,"C:\\fallback.exe");}catch(ArgumentException){rejected=true;}Check(rejected,"reject malformed target flags");}
  Check(PlayerTarget.AssemblyDirectory==Path.GetDirectoryName(typeof(PlayerTarget).Assembly.Location),"assembly directory, not host directory");
  Console.WriteLine("PASS player target argument and location policy");return 0;
 }
}
'@
# Do not use foreach iteration variables as ref arguments in the C# fixture.
$source=$source.Replace('try{PlayerTarget.Parse(ref bad,','try{var badArgs=bad;PlayerTarget.Parse(ref badArgs,')
$test=Join-Path $Stage 'dist\PlayerTargetTests.cs';$exe=Join-Path $Stage 'dist\PlayerTargetTests.exe'
[IO.File]::WriteAllText($test,$source,[Text.UTF8Encoding]::new($false))
& $csc /nologo /out:$exe "$Stage\PlayerTarget.cs" $test
if($LASTEXITCODE){throw 'Player target compile failed'}
& $exe;if($LASTEXITCODE){throw 'Player target policy failed'}
