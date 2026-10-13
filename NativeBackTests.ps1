param([string]$NativeDirectory="$PSScriptRoot\native")
$ErrorActionPreference='Stop'
Set-Location $NativeDirectory
& .\native-test.exe test 1500
if($LASTEXITCODE){throw 'Native concurrent/controller-death test failed'}
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class BackPolicyTest {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr LoadLibrary(string p);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi)] public static extern IntPtr GetProcAddress(IntPtr m,string n);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate uint Test();
 public static uint Run(string p){var h=LoadLibrary(p);if(h==IntPtr.Zero)throw new Exception("Load failed");var f=GetProcAddress(h,"TestStationPolicy");if(f==IntPtr.Zero)throw new Exception("Export absent");return ((Test)Marshal.GetDelegateForFunctionPointer(f,typeof(Test)))();}
}
'@
if([BackPolicyTest]::Run((Join-Path $NativeDirectory 'timedhook.dll')) -ne 0){throw 'Station policy failed'}
'STATION_512_POLICY_PASS'
for($i=0;$i -lt 2;$i++){
 $owner=Start-Process powershell.exe -ArgumentList '-NoProfile -NonInteractive -Command "Start-Sleep -Seconds 30"' -WindowStyle Hidden -PassThru
 $target=Start-Process .\loader-fixture.exe -WorkingDirectory $NativeDirectory -RedirectStandardOutput "owned-$i.out" -RedirectStandardError "owned-$i.err" -PassThru
 try{
  $null=$target.Handle;$null=$owner.Handle
  Start-Sleep -Milliseconds 250
  & .\service-runner.exe --owned-fixture $target.Id $owner.Id
  if($LASTEXITCODE){throw 'Owned loader failed'}
  Start-Sleep -Milliseconds 300
  if($target.HasExited){throw 'Owned fixture exited while owner alive'}
  Stop-Process -Id $owner.Id
  $owner.WaitForExit()
  if(!$target.WaitForExit(10000)){throw 'Owned restoration timed out'}
  if($target.ExitCode -ne 0){throw 'Owned pointer/protection test failed'}
  Get-Content "owned-$i.out"
  "OWNED_DEATH_PASS round=$i"
 }finally{if(!$owner.HasExited){Stop-Process -Id $owner.Id};if(!$target.HasExited){Stop-Process -Id $target.Id}}
}
'ALL NATIVE BACK TESTS PASSED'
