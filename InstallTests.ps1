param([string]$Stage=$PSScriptRoot)
$ErrorActionPreference='Stop'
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$root=Join-Path $env:LOCALAPPDATA 'RockTekRemote\Sandbox'
$setup=Join-Path $Stage 'dist\RockTekRemoteSetup.exe'
if(Test-Path "$root\settings.json"){[IO.File]::Move("$root\settings.json","$root\settings-scope-fixture-backup.json")}
$profileHashesBefore=@{}
foreach($name in @('CloudMus','Murglar')){$file=Join-Path $root ('profiles\'+$name+'.json');if(Test-Path $file){$profileHashesBefore[$name]=(Get-FileHash $file).Hash}}
$p=Start-Process $setup -ArgumentList '--silent','--sandbox' -PassThru -Wait
Assert ($p.ExitCode -eq 0) 'sandbox install exit'
Assert (!(Test-Path "$root\settings.json")) 'fresh install leaves missing EXEs unconfigured until GUI Save'
foreach($name in @('CloudMusPlayPause.exe','MurglarRemote.exe')){Assert (Test-Path (Join-Path $root $name)) ('bundled player helper '+$name);Assert ((Get-FileHash (Join-Path $root $name)).Hash -eq (Get-FileHash (Join-Path $Stage ('dist\'+$name))).Hash) ('exact embedded player helper '+$name)}
foreach($name in @('CloudMus','Murglar')){$file=Join-Path $root ('profiles\'+$name+'.json');Assert (Test-Path $file) ('bundled default profile '+$name);$expected=if($profileHashesBefore.ContainsKey($name)){$profileHashesBefore[$name]}else{(Get-FileHash (Join-Path $Stage ('dist\profiles\'+$name+'.json'))).Hash};Assert ((Get-FileHash $file).Hash -eq $expected) ('embedded fresh profile or preserved existing bytes '+$name)}
[Reflection.Assembly]::LoadFrom((Join-Path $Stage 'dist\RockTekRemote.exe'))|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save("$root\settings.json")
Assert (Test-Path "$root\RockTekRemote.exe") 'embedded app extracted'
Assert ((Get-FileHash "$root\RockTekRemote.exe").Hash -eq (Get-FileHash (Join-Path $Stage 'dist\RockTekRemote.exe')).Hash) 'embedded installer payload hash exactly matches built runtime'
Assert (Test-Path "$root\RockTek Remote.lnk") 'sandbox start shortcut'
$shell=New-Object -ComObject WScript.Shell;$link=$shell.CreateShortcut("$root\RockTek Remote.lnk")
try {Assert ($link.TargetPath -eq "$root\RockTekRemote.exe" -and $link.Arguments -eq ('--settings-only --config "'+$root+'\settings.json"')) 'sandbox shortcut explicitly isolates settings'}finally{[Runtime.InteropServices.Marshal]::ReleaseComObject($link)|Out-Null;[Runtime.InteropServices.Marshal]::ReleaseComObject($shell)|Out-Null}
Assert (Test-Path "$root\uninstall-entry.json") 'sandbox uninstall metadata'
[IO.File]::WriteAllText("$root\user-note.txt",'keep')
$profileFile=Join-Path $root 'profiles\Murglar.json'
$profileBefore=[IO.File]::ReadAllBytes($profileFile)
$modified=[IO.File]::ReadAllText($profileFile).Replace('"Delay": 350','"Delay": 450')
[IO.File]::WriteAllText($profileFile,$modified,[Text.UTF8Encoding]::new($false))
$profileHash=(Get-FileHash $profileFile).Hash
$p=Start-Process $setup -ArgumentList '--silent','--sandbox' -PassThru -Wait
Assert ($p.ExitCode -eq 0 -and (Get-FileHash $profileFile).Hash -eq $profileHash) 'upgrade preserves edited bundled profile bytes'
[IO.File]::WriteAllBytes($profileFile,$profileBefore)
$p=Start-Process "$root\RockTekRemote.exe" -ArgumentList '--uninstall','--silent','--sandbox' -PassThru -Wait
Assert ($p.ExitCode -eq 0) 'sandbox uninstall exit'
Start-Sleep -Seconds 3
Assert (!(Test-Path "$root\RockTekRemote.exe")) 'owned app removed'
foreach($name in @('CloudMusPlayPause.exe','MurglarRemote.exe')){Assert (!(Test-Path (Join-Path $root $name))) ('owned player helper removed '+$name)}
Assert (Test-Path (Join-Path $root 'profiles\Murglar.json')) 'editable player profiles survive uninstall'
Assert (!(Test-Path "$root\RockTek Remote.lnk")) 'owned shortcut removed'
Assert (Test-Path "$root\user-note.txt") 'unknown file preserved'
Assert (Test-Path "$root\settings.json") 'settings preserved'
Remove-Item "$root\user-note.txt"
$settingsBefore=[IO.File]::ReadAllText("$root\settings.json")
[IO.File]::WriteAllText("$root\settings.json",'{bad')
$p=Start-Process $setup -ArgumentList '--silent','--sandbox' -PassThru -Wait
Assert ($p.ExitCode -ne 0) 'invalid settings abort installation'
Assert (!(Test-Path "$root\RockTekRemote.exe")) 'failed install leaves no partial payload'
[IO.File]::WriteAllText("$root\settings.json",$settingsBefore)
if(Test-Path "$root\settings-scope-fixture-backup.json"){Remove-Item "$root\settings.json";[IO.File]::Move("$root\settings-scope-fixture-backup.json","$root\settings.json")}
'ALL INSTALL TESTS PASSED'
