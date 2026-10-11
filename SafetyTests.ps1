param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$fixture=Join-Path $PSScriptRoot 'SafetyFixture';New-Item -ItemType Directory -Force $fixture|Out-Null
$target=Join-Path $fixture 'target';New-Item -ItemType Directory -Force $target|Out-Null
[IO.File]::WriteAllText("$target\settings.json",'sentinel')
$link=Join-Path $fixture 'redirect';New-Item -ItemType Junction -Path $link -Target $target|Out-Null
try {$bad=$false;try{[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save("$link\settings.json")}catch{$bad=$true};Assert ($bad -and [IO.File]::ReadAllText("$target\settings.json") -eq 'sentinel') 'junction config write refused; target unchanged'}finally{[IO.Directory]::Delete($link)}
$one=Join-Path $env:USERPROFILE 'OneDrive';if(Test-Path $one){[FileSafety]::CheckPath((Join-Path $one 'RockTekRemote\settings.json'));Assert $true 'existing OneDrive path allowed (read-only)'}
$root=[Installation]::Root($true);New-Item -ItemType Directory -Force $root|Out-Null
$settings=Join-Path $root 'settings.json';$previousSettings=if(Test-Path $settings){[IO.File]::ReadAllText($settings)}else{$null}
try {
[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save($settings)
$predictable=Join-Path $root 'RockTekRemote.exe.new';[IO.File]::WriteAllText($predictable,'keep predictable')
$p=Start-Process "$PSScriptRoot\dist\RockTekRemoteSetup.exe" -ArgumentList '--silent','--sandbox' -Wait -PassThru
Assert ($p.ExitCode -eq 0 -and [IO.File]::ReadAllText($predictable) -eq 'keep predictable') 'UUID payload leaves predictable exe.new untouched'
$marker=Join-Path $root 'install.marker';$markerTarget=Join-Path $fixture 'marker-target.txt';[IO.File]::WriteAllText($markerTarget,'RockTekRemote per-user v2');Remove-Item $marker
New-Item -ItemType SymbolicLink -Path $marker -Target $markerTarget|Out-Null
try {$hash=(Get-FileHash "$root\RockTekRemote.exe").Hash;$p=Start-Process "$PSScriptRoot\dist\RockTekRemoteSetup.exe" -ArgumentList '--silent','--sandbox' -Wait -PassThru;Assert ($p.ExitCode -ne 0 -and [IO.File]::ReadAllText($markerTarget) -eq 'RockTekRemote per-user v2' -and (Get-FileHash "$root\RockTekRemote.exe").Hash -eq $hash) 'marker redirect refused; marker target and exe unchanged'}finally{[IO.File]::Delete($marker);[IO.File]::WriteAllText($marker,'RockTekRemote per-user v2');Remove-Item $predictable}
$safeRoot=[Installation].GetMethod('SafeRoot',[Reflection.BindingFlags]'NonPublic,Static')
$bad=$false;try{$safeRoot.Invoke($null,@([string]'C:\Windows'))}catch{$bad=$true}
Assert $bad 'installer rejects paths outside current-user local application data'
Assert ($null -eq [Installation].GetMethod('MigrateLegacyFixture',[Reflection.BindingFlags]'NonPublic,Static')) 'installer contains no fixed scheduled-task migration'
Remove-Item $fixture -Recurse -Force
} finally {if($null -ne $previousSettings){[IO.File]::WriteAllText($settings,$previousSettings)}else{Remove-Item $settings -ErrorAction SilentlyContinue}}
'ALL SAFETY TESTS PASSED'
