param([string]$OutputDirectory="$PSScriptRoot\dist")
$ErrorActionPreference='Stop'
foreach($name in @('timedhook.dll','service-runner.exe')){if(!(Test-Path (Join-Path $PSScriptRoot ('native\'+$name)))){throw 'Build native payload on Linux with python3 native/build.py first'}}
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path $csc)){throw '.NET Framework 4.x compiler not found'}
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$app=Join-Path $OutputDirectory 'RockTekRemote.exe'
$common=@('/nologo','/target:winexe','/platform:x64','/optimize+',('/win32manifest:'+$PSScriptRoot+'\assets\app.manifest'),('/win32icon:'+$PSScriptRoot+'\assets\app-icon.ico'),('/resource:'+$PSScriptRoot+'\assets\app-icon.ico,app-icon.ico'),'/r:System.Windows.Forms.dll')
$native=@(('/resource:'+$PSScriptRoot+'\native\timedhook.dll,timedhook.dll'),('/resource:'+$PSScriptRoot+'\native\service-runner.exe,service-runner.exe'),'/r:System.Drawing.dll','/r:System.Web.Extensions.dll')
$appSources=@('RockTekRemote','HidConsumerReader','InputDiagnostics','ActionExecutor','RemoteMapping','HoldRepeater','RemoteConfig','ActionModel','AppIcon','ActionEditor','SettingsForm','NativeBackSupport','Installation','FileSafety','ConsumerInput','ConfigLocation','WindowLayout','UiText','DeviceFilter','PlayerPayloads')|ForEach-Object{Join-Path $PSScriptRoot ($_+'.cs')}
& $csc @common @native ('/out:'+$app) @appSources
if($LASTEXITCODE){throw 'Application compile failed'}
$cloud=Join-Path $OutputDirectory 'CloudMusPlayPause.exe';$murglar=Join-Path $OutputDirectory 'MurglarRemote.exe'
$wpf=Join-Path (Split-Path $csc) 'WPF'
& $csc @common ('/out:'+$cloud) ('/r:'+$app) ('/r:'+$wpf+'\UIAutomationClient.dll') ('/r:'+$wpf+'\UIAutomationTypes.dll') ('/r:'+$wpf+'\WindowsBase.dll') "$PSScriptRoot\CloudMusPlayPause.cs" "$PSScriptRoot\CloudMusOptions.cs" "$PSScriptRoot\PlayerTarget.cs" "$PSScriptRoot\PlayerClose.cs"
if($LASTEXITCODE){throw 'CloudMus bridge compile failed'}
& $csc @common ('/out:'+$murglar) /r:System.Management.dll "$PSScriptRoot\MurglarRemote.cs" "$PSScriptRoot\PlayerTarget.cs" "$PSScriptRoot\PlayerClose.cs"
if($LASTEXITCODE){throw 'Murglar bridge compile failed'}
New-Item -ItemType Directory -Force "$OutputDirectory\profiles"|Out-Null
Copy-Item "$PSScriptRoot\profiles\CloudMus.json","$PSScriptRoot\profiles\Murglar.json" "$OutputDirectory\profiles"
$setupSources=@('Setup','NativeBackSupport','Installation','FileSafety','ConsumerInput','ConfigLocation','WindowLayout','UiText','DeviceFilter','RemoteConfig','ActionModel','AppIcon','ActionEditor','SettingsForm','PlayerPayloads')|ForEach-Object{Join-Path $PSScriptRoot ($_+'.cs')}
& $csc @common @native ('/out:'+$OutputDirectory+'\RockTekRemoteSetup.exe') ('/resource:'+$app+',RockTekRemote.exe') ('/resource:'+$cloud+',CloudMusPlayPause.exe') ('/resource:'+$murglar+',MurglarRemote.exe') ('/resource:'+$PSScriptRoot+'\profiles\CloudMus.json,profiles.CloudMus.json') ('/resource:'+$PSScriptRoot+'\profiles\Murglar.json,profiles.Murglar.json') @setupSources
if($LASTEXITCODE){throw 'Setup compile failed'}
$portable=Join-Path $OutputDirectory 'RockTekRemote-portable.zip'
if(Test-Path $portable){Remove-Item $portable}
Compress-Archive -LiteralPath $app,$cloud,$murglar,"$OutputDirectory\profiles" -DestinationPath $portable
$names=@('RockTekRemote.exe','RockTekRemoteSetup.exe','CloudMusPlayPause.exe','MurglarRemote.exe','profiles/CloudMus.json','profiles/Murglar.json','RockTekRemote-portable.zip')
$lines=$names|ForEach-Object{((Get-FileHash (Join-Path $OutputDirectory $_) -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+$_}
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'SHA256SUMS'),[string[]]$lines,[Text.UTF8Encoding]::new($false))
$lines
