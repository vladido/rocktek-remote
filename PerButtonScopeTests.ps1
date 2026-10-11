param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
Add-Type -AssemblyName System.Windows.Forms
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$c=[RemoteConfig]::Parse('{"MurglarPath":"..\\Apps\\old.exe"}')
Assert ($c.Buttons['Left'].ScopeApplicationPath -eq '..\Apps\old.exe') 'legacy restricted scope copied verbatim per button'
Assert ($c.Buttons['Home'].ApplicationPath -eq '..\Apps\old.exe') 'opening migration remains independent'
Assert (!$c.Json().Contains('"TargetApplicationPath"') -and !$c.Json().Contains('"MurglarPath"')) 'canonical JSON has no global application field'
$root=Join-Path $PSScriptRoot 'ScopeFixture';New-Item -ItemType Directory -Force $root|Out-Null
try {
 $p=Join-Path $root 'settings.json';$c.Save($p);$f=[SettingsForm]::new($p)
 try {
  $flags=[Reflection.BindingFlags]'NonPublic,Instance'
  Assert ($null -eq $f.GetType().GetField('player',$flags)) 'global EXE editor removed'
  $scope=$f.GetType().GetField('scope',$flags).GetValue($f)
  Assert ($scope.GetItemText('Global') -ceq 'Везде') 'exact everywhere label'
  Assert ($scope.GetItemText('Application') -ceq 'Только в выбранном приложении') 'exact restricted label'
  $f.SelectButton('Left');$f.SetScopeApplicationPath('left.exe');$f.SetEditor('Key','Ctrl+Right','Application',$true,650,200)
  $f.SelectButton('Right');$f.SetScopeApplicationPath('right.exe')
  Assert ($f.SaveCurrent()) 'different EXEs save'
  $saved=[RemoteConfig]::Load($p)
  Assert ($saved.Buttons['Left'].ScopeApplicationPath -eq 'left.exe' -and $saved.Buttons['Right'].ScopeApplicationPath -eq 'right.exe') 'selection stores independent scope EXEs'
  $f.SelectButton('Home');$f.SetEditor('LaunchApplication','Space','Application',$false,350,150);$f.SetScopeApplicationPath('scope.exe');$a=[ActionSpec]::new();$a.Action='LaunchApplication';$a.ApplicationPath='open.exe';$a.Arguments='--keep';$a.WorkingDirectory='.';$f.SetActionDetails($a)
  Assert ($f.SaveCurrent()) 'restricted opening save'
 }finally{$f.Dispose()}
 $f=[SettingsForm]::new($p);try{$f.SelectButton('Home');Assert ($f.EditorScopeApplicationPath -eq 'scope.exe' -and $f.EditorActionDetails.ApplicationPath -eq 'open.exe') 'reopen separates opening EXE from scope EXE';$f.SetScopeApplicationPath('');Assert (!$f.SaveCurrent()) 'empty restricted scope save refused'}finally{$f.Dispose()}
 $cfg=[RemoteConfig]::Load($p);$cfg.Buttons['Left'].ScopeApplicationPath='\\server\bad.exe';$bad=$false;try{$cfg.Validate()}catch{$bad=$true};Assert $bad 'unsafe scope EXE refused'
 $bad=$false;try{[RemoteConfig]::Parse('{"MurglarPath":""}')|Out-Null}catch{$bad=$true};Assert $bad 'missing legacy target cannot broaden restricted scopes'
 $draft=[RemoteConfig]::Load($p);$draft.Buttons['Left'].ScopeApplicationPath='';$draft.Buttons['Left'].Key='Alt+F2';$draft.Buttons['Left'].Delay=750;$draft.TargetApplicationPath='fallback.exe';[IO.File]::WriteAllText($p,([Web.Script.Serialization.JavaScriptSerializer]::new().Serialize($draft)));$bad=$false;try{[RemoteConfig]::Load($p)|Out-Null}catch{$bad=$true};Assert $bad 'explicit empty scope does not inherit even populated legacy target';$warning='';$editable=[RemoteConfig]::LoadSafe($p,[ref]$warning)
 Assert ($warning.Length -gt 0 -and $editable.Buttons['Left'].Scope -eq 'Application' -and $editable.Buttons['Left'].Key -eq 'Alt+F2' -and $editable.Buttons['Left'].Delay -eq 750) 'unconfigured editor preserves assignments with clear configure warning'
}finally{Remove-Item $root -Recurse -Force}
'ALL PER BUTTON SCOPE TESTS PASSED'
