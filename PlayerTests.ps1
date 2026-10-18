param([string]$Stage=$PSScriptRoot)
$ErrorActionPreference='Stop'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$Stage\PlayerTargetTests.ps1" -Stage $Stage
if($LASTEXITCODE){throw 'Player target tests failed'}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$Stage\PlayerPackageTests.ps1" -Stage $Stage
if($LASTEXITCODE){throw 'Player package tests failed'}
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$app=Join-Path $Stage 'dist\RockTekRemote.exe'
& $csc /nologo ('/r:'+$app) ('/out:'+$Stage+'\dist\CloudMusOptionsTests.exe') "$Stage\CloudMusOptions.cs" "$Stage\CloudMusOptionsTests.cs"
if($LASTEXITCODE){throw 'CloudMus option tests compile failed'}
& "$Stage\dist\CloudMusOptionsTests.exe";if($LASTEXITCODE){throw 'CloudMus option policy failed'}
foreach($test in @('MurglarTests','RatingsTests')){
 & $csc /nologo /platform:x64 /r:System.Windows.Forms.dll /r:System.Management.dll ('/main:'+$test) ('/out:'+$Stage+'\dist\'+$test+'.exe') "$Stage\$test.cs" "$Stage\MurglarRemote.cs" "$Stage\PlayerTarget.cs"
 if($LASTEXITCODE){throw ($test+' compile failed')}
 & "$Stage\dist\$test.exe";if($LASTEXITCODE){throw ($test+' failed')}
}
'ALL PLAYER TESTS PASSED'
