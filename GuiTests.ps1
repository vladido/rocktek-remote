param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$p=Join-Path $PSScriptRoot 'gui-settings.json'
[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save($p)
$f=New-Object SettingsForm($p)
try {
$beforeSelection=[IO.File]::ReadAllText($p)
$memoryField=$f.GetType().GetField('config',[Reflection.BindingFlags]'NonPublic,Instance')
$beforeMemory=$memoryField.GetValue($f).Json()
$inputLabel=$f.GetType().GetField('inputInfo',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($f)
$seen=New-Object 'Collections.Generic.HashSet[string]'
foreach($name in @('Home','Back','Right')){
 $f.SelectButton($name)
 $saved=[RemoteConfig]::Load($p).Buttons[$name]
 $expected='Мультимедиа · код 0x'+$saved.Usage.ToString('X4')
 Assert ($inputLabel.Text -ceq $expected) "SelectButton $name shows exact saved Consumer usage"
 Assert ($seen.Add($inputLabel.Text)) "SelectButton $name input code is distinct"
}
Assert ([IO.File]::ReadAllText($p) -ceq $beforeSelection) 'selecting buttons preserves settings bytes'
Assert ($memoryField.GetValue($f).Json() -ceq $beforeMemory) 'selecting buttons preserves all in-memory mappings'
$f.SelectButton('Left')
$f.SetEditor('Key','Ctrl+Right','Global',$true,650,200)
Assert ($f.SaveCurrent()) 'GUI save succeeds'
$c=[RemoteConfig]::Load($p);$b=$c.Buttons['Left']
Assert ($b.Action -eq 'Key' -and $b.Key -eq 'Ctrl+Right' -and $b.Scope -eq 'Global' -and $b.Repeat -and $b.Delay -eq 650 -and $b.Interval -eq 200) 'GUI writes all six fields to exact settings file'
$before=[IO.File]::ReadAllText($p)
$f.SetEditor('Key','Ctrl+Unicorn','Global',$true,650,200)
Assert (!$f.SaveCurrent()) 'GUI rejects invalid combination'
Assert ([IO.File]::ReadAllText($p) -eq $before) 'invalid GUI save is atomic'
$f.SetEditor('Key','Enter','Application',$false,350,150)
Assert ($f.SaveCurrent()) 'GUI can change Enter and disable hold'
$f.Dispose();$f=New-Object SettingsForm($p);$f.SelectButton('Left')
Assert ($f.EditorKey -eq 'Enter') 'GUI reload reads same file'
} finally {$f.Dispose();Remove-Item $p -ErrorAction SilentlyContinue}
'ALL GUI TESTS PASSED'
