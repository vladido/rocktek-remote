param([string]$SetupPath=(Join-Path $PSScriptRoot 'dist\RockTekRemoteSetup.exe'))
$ErrorActionPreference='Stop'
if(!(Test-Path -LiteralPath $SetupPath -PathType Leaf)){throw 'Installer not found. Build the project or provide -SetupPath.'}
$p=Start-Process -FilePath $SetupPath -ArgumentList '--silent' -Wait -PassThru
if($p.ExitCode){throw 'RockTek Remote installation failed.'}
$exe=Join-Path $env:LOCALAPPDATA 'Programs\RockTekRemote\RockTekRemote.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Installed application not found.'}
Write-Output 'Installed. Open settings from the Start menu. Autostart begins at the next Windows sign-in.'
