param([string]$Stage=$PSScriptRoot)
$ErrorActionPreference='Stop'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$app=Join-Path $Stage 'dist\RockTekRemote.exe'
& $csc /nologo ('/r:'+$app) ('/out:'+$Stage+'\dist\ShutdownDispatchTests.exe') "$Stage\ShutdownDispatchTests.cs"
if($LASTEXITCODE){throw 'Shutdown recording adapter compile failed'}
& "$Stage\dist\ShutdownDispatchTests.exe"
if($LASTEXITCODE){throw 'Shutdown dispatch test failed'}
