param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$a=[Reflection.Assembly]::LoadFrom($Assembly)
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
Assert ($null -ne ('ConfigLocation' -as [type])) 'persistent primary location API exists'
$root=Join-Path $PSScriptRoot 'LocationFixture';New-Item -ItemType Directory -Force $root|Out-Null
$old=Join-Path $root 'old\settings.json';$new=Join-Path $root 'new\settings.json';$pointer=Join-Path $root 'config-location.json'
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}');$c.TargetApplicationPath='..\Example\Example.exe';foreach($button in $c.Buttons.Values){if($button.Scope -eq 'Application'){$button.ScopeApplicationPath=$c.TargetApplicationPath}};$c.Buttons['Left'].Key='Ctrl+Right';$c.Save($old)
$f=[SettingsForm]::new($old,$true,$pointer)
try {
Assert ($f.SelectSettingsLocation($new,$false)) 'create new primary config then atomically commit pointer'
$warning='';$resolved=[AppPaths]::SettingsWithLocator([string[]]@(),$pointer,[ref]$warning)
Assert ($resolved -eq $new -and !$warning) 'default discovery follows selected primary'
Assert ([RemoteConfig]::Load($new).Buttons['Left'].ScopeApplicationPath -eq [AppPaths]::Player($c,$old)) 'moving config preserves relative player meaning'
$f.SelectButton('Left');$f.SetEditor('Key','Enter','Application',$true,500,180)
Assert ($f.SaveCurrent() -and [RemoteConfig]::Load($new).Buttons['Left'].Key -eq 'Enter') 'actual Save writes newly selected path'
Assert ([RemoteConfig]::Load($old).Buttons['Left'].Key -eq 'Ctrl+Right') 'old settings remain unchanged'
$f.Dispose();$f=[SettingsForm]::new($resolved,$true,$pointer);$f.SelectButton('Left')
Assert ($f.EditorKey -eq 'Enter' -and $f.SettingsPath -eq $new) 'editor reopens exact selected settings'
$before=[IO.File]::ReadAllText($pointer);$oldBytes=[IO.File]::ReadAllText($old)
Assert (!$f.SelectSettingsLocation($old,$false) -and [IO.File]::ReadAllText($pointer) -eq $before) 'existing config requires explicit load decision'
Assert ($f.SelectSettingsLocation($old,$true) -and [IO.File]::ReadAllText($old) -eq $oldBytes) 'existing valid settings selected without overwriting'
$bad=Join-Path $root 'bad.json';[IO.File]::WriteAllText($bad,'{bad')
$before=[IO.File]::ReadAllText($pointer)
Assert (!$f.SelectSettingsLocation($bad,$true) -and [IO.File]::ReadAllText($pointer) -eq $before -and [IO.File]::ReadAllText($bad) -eq '{bad') 'invalid chosen JSON leaves pointer and file intact'
$isolated=[SettingsForm]::new($new)
try {Assert (!$isolated.SelectSettingsLocation($old,$true) -and [IO.File]::ReadAllText($pointer) -eq $before) 'explicit isolated GUI cannot change primary locator'}finally{$isolated.Dispose()}
$warning='';Assert ([AppPaths]::SettingsWithLocator([string[]]@('--config',$new),$pointer,[ref]$warning) -eq $new -and [IO.File]::ReadAllText($pointer) -eq $before) 'explicit config discovery bypasses locator without mutation'
# Runtime re-resolves fixture locator live, no constructor/hooks/injection.
$t=$a.GetType('RockTekRemote');$bf=[Reflection.BindingFlags]'NonPublic,Instance,Static';$o=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($t)
function SetField($n,$v){$t.GetField($n,$bf).SetValue($o,$v)}
function GetField($n){$t.GetField($n,$bf).GetValue($o)}
SetField 'Clock' ([Diagnostics.Stopwatch]::StartNew());SetField 'Repeater' (New-Object HoldRepeater);SetField 'ConfigUsesLocator' $true;SetField 'LocatorFile' $pointer;SetField 'ConfigPath' $new;SetField 'LogFile' (Join-Path $root 'runtime.log')
$t.GetMethod('Reload',$bf).Invoke($o,@())|Out-Null
Assert ((GetField 'ConfigPath') -eq $old -and (GetField 'Config').Buttons['Left'].Key -eq 'Ctrl+Right') 'runtime reads current locator target'
[ConfigLocation]::Commit($pointer,$new);$t.GetMethod('Reload',$bf).Invoke($o,@())|Out-Null
Assert ((GetField 'ConfigPath') -eq $new -and (GetField 'Config').Buttons['Left'].Key -eq 'Enter') 'runtime live change consumes selected file'
[IO.File]::WriteAllText($pointer,'{broken locator');$warning='';$resolved=[AppPaths]::SettingsWithLocator([string[]]@(),$pointer,[ref]$warning)
Assert ($resolved -eq [AppPaths]::DefaultSettings() -and $warning -match 'Файл настроек') 'invalid locator fallback has clear Russian warning'
$t.GetMethod('Reload',$bf).Invoke($o,@())|Out-Null
Assert ((GetField 'ConfigPath') -eq $new) 'runtime retains last valid selection on invalid locator'
$f.Dispose();$f=[SettingsForm]::new($old,$true,$pointer)
Assert (!$f.SaveCurrent() -and [IO.File]::ReadAllText($old) -eq $oldBytes) 'invalid locator blocks silent fallback overwrite'
Assert ($f.SelectSettingsLocation($new,$true) -and $f.SaveCurrent()) 'explicit valid reselect recovers invalid locator'
$before=[IO.File]::ReadAllText($pointer)
Assert (!$f.SelectSettingsLocation($pointer,$false) -and [IO.File]::ReadAllText($pointer) -eq $before) 'locator cannot also be selected as settings'
$self=Join-Path $root 'self.json';[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save($self)
$bytes=[IO.File]::ReadAllText($self);$refused=$false;try{[ConfigLocation]::Commit($self,$self)}catch{$refused=$true}
Assert ($refused -and [IO.File]::ReadAllText($self) -eq $bytes) 'commit refuses locator/settings alias without destroying JSON'
$target=Join-Path $root 'junction-target';New-Item -ItemType Directory -Force $target|Out-Null
$junction=Join-Path $root 'redirect';New-Item -ItemType Junction -Path $junction -Target $target|Out-Null
try {
 $before=[IO.File]::ReadAllText($pointer)
 Assert (!$f.SelectSettingsLocation((Join-Path $junction 'settings.json'),$false) -and [IO.File]::ReadAllText($pointer) -eq $before -and !(Test-Path (Join-Path $target 'settings.json'))) 'redirected settings parent refuses write and pointer change'
 $priorOne=$env:OneDriveConsumer
 try {$env:OneDriveConsumer=$junction;$rejected=$false;$msg='';try{[AppPaths]::SettingsWithLocator([string[]]@(),(Join-Path $root 'absent-pointer.json'),[ref]$msg)|Out-Null}catch{$rejected=$true};Assert $rejected 'redirected default path cannot bypass locator safety'}finally{[Environment]::SetEnvironmentVariable('OneDriveConsumer',$priorOne,'Process')}
 $unsafe=[SettingsForm]::new($new,$true,(Join-Path $junction 'config-location.json'))
 try {Assert (!$unsafe.SelectSettingsLocation((Join-Path $root 'never-created.json'),$false) -and !(Test-Path (Join-Path $root 'never-created.json'))) 'unsafe locator refused before creating new settings'}finally{$unsafe.Dispose()}
}finally{[IO.Directory]::Delete($junction)}
Assert ([AppPaths]::DefaultSettings() -ne $new) 'default settings discovery remains independent of explicit locator selection'
$stale=[SettingsForm]::new($old,$true,$pointer)
try {Assert (!$stale.SaveCurrent() -and [IO.File]::ReadAllText($old) -eq $oldBytes) 'stale editor refuses save to previous primary path'}finally{$stale.Dispose()}
$raw=[IO.File]::ReadAllText($pointer)|ConvertFrom-Json
Assert ($raw.SettingsPath -eq $new -and @($raw.PSObject.Properties).Count -eq 2) 'locator contains only version and path metadata'
}finally{$f.Dispose();Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue}
'ALL LOCATION TESTS PASSED (isolated fixture locator; no production pointer writes)'
