param([string]$OutputDirectory="$PSScriptRoot\dist")
$ErrorActionPreference='Stop'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path $csc)){throw '.NET Framework 4.x compiler not found'}
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$app=Join-Path $OutputDirectory 'RockTekRemote.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /win32icon:"$PSScriptRoot\assets\app-icon.ico" /resource:"$PSScriptRoot\assets\app-icon.ico,app-icon.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /out:$app "$PSScriptRoot\RockTekRemote.cs" "$PSScriptRoot\HidConsumerReader.cs" "$PSScriptRoot\InputDiagnostics.cs" "$PSScriptRoot\ActionExecutor.cs" "$PSScriptRoot\RemoteMapping.cs" "$PSScriptRoot\HoldRepeater.cs" "$PSScriptRoot\RemoteConfig.cs" "$PSScriptRoot\ActionModel.cs" "$PSScriptRoot\AppIcon.cs" "$PSScriptRoot\ActionEditor.cs" "$PSScriptRoot\SettingsForm.cs" "$PSScriptRoot\Installation.cs" "$PSScriptRoot\FileSafety.cs" "$PSScriptRoot\ConsumerInput.cs" "$PSScriptRoot\ConfigLocation.cs" "$PSScriptRoot\WindowLayout.cs" "$PSScriptRoot\UiText.cs" "$PSScriptRoot\DeviceFilter.cs"
if($LASTEXITCODE){throw 'Application compile failed'}
& $csc /nologo /target:winexe /platform:x64 /optimize+ /win32icon:"$PSScriptRoot\assets\app-icon.ico" /resource:"$PSScriptRoot\assets\app-icon.ico,app-icon.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /out:"$OutputDirectory\RockTekRemoteSetup.exe" /resource:"$app,RockTekRemote.exe" "$PSScriptRoot\Setup.cs" "$PSScriptRoot\Installation.cs" "$PSScriptRoot\FileSafety.cs" "$PSScriptRoot\ConsumerInput.cs" "$PSScriptRoot\ConfigLocation.cs" "$PSScriptRoot\WindowLayout.cs" "$PSScriptRoot\UiText.cs" "$PSScriptRoot\DeviceFilter.cs" "$PSScriptRoot\RemoteConfig.cs" "$PSScriptRoot\ActionModel.cs" "$PSScriptRoot\AppIcon.cs" "$PSScriptRoot\ActionEditor.cs" "$PSScriptRoot\SettingsForm.cs"
if($LASTEXITCODE){throw 'Setup compile failed'}
Get-FileHash $app,"$OutputDirectory\RockTekRemoteSetup.exe" -Algorithm SHA256 | Format-Table
$portable=Join-Path $OutputDirectory 'RockTekRemote-portable.zip'
if(Test-Path $portable){Remove-Item $portable}
Compress-Archive -LiteralPath $app -DestinationPath $portable
$lines=@('RockTekRemote.exe','RockTekRemoteSetup.exe','RockTekRemote-portable.zip')|ForEach-Object{((Get-FileHash (Join-Path $OutputDirectory $_) -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+$_}
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'SHA256SUMS'),[string[]]$lines,[Text.UTF8Encoding]::new($false))
