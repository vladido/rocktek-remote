param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference="Stop"
$env:ROCKTEK_REMOTE_DEVICE="AABBCCDDEEFF"
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\NativeBackTests.ps1";if($LASTEXITCODE){throw "Native Back tests failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\DeviceSelectionTests.ps1" -Assembly $Assembly;if($LASTEXITCODE){throw "Device selection/localization tests failed"}
$env:ROCKTEK_REMOTE_LANGUAGE="ru"
$app=(Resolve-Path $Assembly).Path
$testOutput=Split-Path $app
$csc=Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /out:"$testOutput\MappingTests.exe" "$PSScriptRoot\MappingTests.cs" "$PSScriptRoot\RemoteMapping.cs" "$PSScriptRoot\DeviceFilter.cs"
if($LASTEXITCODE){throw 'Mapping test compile failed'}
& $csc /nologo /out:"$testOutput\HoldTests.exe" "$PSScriptRoot\HoldTests.cs" "$PSScriptRoot\HoldRepeater.cs"
if($LASTEXITCODE){throw 'Hold test compile failed'}
& "$testOutput\MappingTests.exe";if($LASTEXITCODE){throw 'Mapping tests failed'}
& "$testOutput\HoldTests.exe";if($LASTEXITCODE){throw 'Hold tests failed'}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\ConfigTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\GuiTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& $csc /nologo /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:$app /out:"$testOutput\ActionBehaviorTests.exe" "$PSScriptRoot\ActionBehaviorTests.cs"
if($LASTEXITCODE){throw 'Action behavior test compile failed'}
& "$testOutput\ActionBehaviorTests.exe";if($LASTEXITCODE){throw 'Action behavior tests failed'}
& $csc /nologo /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:$app /out:"$testOutput\PerButtonRuntimeTests.exe" "$PSScriptRoot\PerButtonRuntimeTests.cs"
if($LASTEXITCODE){throw 'Scope runtime test compile failed'}
& "$testOutput\PerButtonRuntimeTests.exe";if($LASTEXITCODE){throw 'Scope runtime tests failed'}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\PerButtonScopeTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\UnifiedApplicationTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\ActionTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\IconTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\ShortcutTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\RuntimeTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\DynamicTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\LearnTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\KeyboardTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\LocationTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\ButtonLayoutTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\WindowTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& $csc /nologo /out:"$testOutput\InputDiagnosticsTests.exe" "$PSScriptRoot\InputDiagnosticsTests.cs" "$PSScriptRoot\InputDiagnostics.cs"
if($LASTEXITCODE){throw 'Input diagnostics tests compile failed'}
& "$testOutput\InputDiagnosticsTests.exe";if($LASTEXITCODE){throw 'Input diagnostics tests failed'}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\RawBoundaryTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "Raw boundary tests failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\IntakeDiagnosticsTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "PowerShell regression failed"}
& $csc /nologo /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:$app /out:"$testOutput\WakeHomeTests.exe" "$PSScriptRoot\WakeHomeTests.cs"
if($LASTEXITCODE){throw 'Wake Home test compile failed'}
& "$testOutput\WakeHomeTests.exe";if($LASTEXITCODE){throw 'Wake Home tests failed'}
& $csc /nologo /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:$app /out:"$testOutput\NativeWakeRegressionTests.exe" "$PSScriptRoot\NativeWakeRegressionTests.cs"
if($LASTEXITCODE){throw 'Native wake regression compile failed'}
& "$testOutput\NativeWakeRegressionTests.exe";if($LASTEXITCODE){throw 'Native wake regression failed'}
& $csc /nologo /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:$app /out:"$testOutput\NativeBackRegressionTests.exe" "$PSScriptRoot\NativeBackRegressionTests.cs"
if($LASTEXITCODE){throw 'Native Back regression compile failed'}
& "$testOutput\NativeBackRegressionTests.exe";if($LASTEXITCODE){throw 'Native Back regression failed'}
foreach($name in @('HidTransport','Foreground','WindowKey')) {
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot ($name+'Tests.ps1')) -Assembly $app
 if($LASTEXITCODE){throw ($name+' regression failed')}
}
foreach($name in @('HidRuntime','ScopedWindow')) {
 & $csc /nologo /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:$app ('/out:'+ $testOutput+'\'+$name+'Tests.exe') (Join-Path $PSScriptRoot ($name+'Tests.cs'))
 if($LASTEXITCODE){throw ($name+' compile failed')}
 & (Join-Path $testOutput ($name+'Tests.exe'));if($LASTEXITCODE){throw ($name+' regression failed')}
}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\SafetyTests.ps1" -Assembly $app;if($LASTEXITCODE){throw "Safety tests failed"}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\InstallTests.ps1" -Stage $PSScriptRoot;if($LASTEXITCODE){throw "Install tests failed"}
