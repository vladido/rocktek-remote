param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
function Controls($c){foreach($child in $c.Controls){$child;Controls $child}}
$p=Join-Path $PSScriptRoot 'language-gui-settings.json'
[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save($p)
$before=(Get-FileHash $p).Hash
$pref=Join-Path $PSScriptRoot 'language-gui-preference.txt'
[UiText]::Initialize([string[]]@('--language','en'),$pref)
$f=[SettingsForm]::new($p)
try {
 $select=@(Controls $f|Where-Object {$_ -is [Windows.Forms.ComboBox] -and $_.AccessibleName -eq 'Язык / Language'})
 Assert ($select.Count -eq 1) 'language selector is discoverable by its bilingual accessible name'
 $f.Show();[Windows.Forms.Application]::DoEvents()
 $language=$select[0]
 Assert ($language.Visible -and $language.Parent.ClientRectangle.Contains($language.Bounds)) 'selector is fully visible within its header'
 $f.SelectButton('Right');$f.SetEditor('Key','Ctrl+Right','Global',$true,500,125)
 $memory=[SettingsForm].GetField('config',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($f).Json()
 $language.SelectedIndex=1;[Windows.Forms.Application]::DoEvents()
 Assert ($f.Text -eq 'RockTek Remote · Настройки') 'switching to Russian updates the live window'
 $actionControl=@(Controls $f|Where-Object {$_ -is [Windows.Forms.ComboBox] -and $_.Items.Contains('LaunchApplication')})[0]
 Assert ($actionControl.Text -eq 'Клавиша / сочетание') 'visible selected action text switches to Russian'
 Assert ($f.EditorKey -eq 'Ctrl+Right') 'language switching preserves unsaved shortcut text'
 Assert ($memory -eq [SettingsForm].GetField('config',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($f).Json()) 'switching language does not change in-memory button assignments'
 Assert ((Get-FileHash $p).Hash -eq $before) 'language switching leaves settings bytes unchanged'
 Assert ([IO.File]::ReadAllText($pref).Trim() -eq 'ru') 'real selector persists Russian'
 $language.SelectedIndex=0;[Windows.Forms.Application]::DoEvents()
 Assert ($f.Text -eq 'RockTek Remote · Settings') 'switching back to English updates the live window'
 $actionControl=@(Controls $f|Where-Object {$_ -is [Windows.Forms.ComboBox] -and $_.Items.Contains('LaunchApplication')})[0]
 Assert ($actionControl.Text -eq 'Key / shortcut') 'visible selected action text refreshes after switching language'
 Assert ((Get-FileHash $p).Hash -eq $before) 'round-trip leaves settings bytes unchanged'
}finally{$f.Dispose();Remove-Item $p,$pref -ErrorAction SilentlyContinue}
'ALL LANGUAGE GUI TESTS PASSED'
