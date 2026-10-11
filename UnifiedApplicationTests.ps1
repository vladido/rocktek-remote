param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
Add-Type -AssemblyName System.Windows.Forms
function Assert($v,$s){if(!$v){throw "FAIL $s"};Write-Output "PASS $s"}
function FindCaption($control,$text){if($control -is [Windows.Forms.Label] -and $control.Text -ceq $text){return $control};foreach($child in $control.Controls){$found=FindCaption $child $text;if($null -ne $found){return $found}};return $null}
Assert ([ActionEditorPanel]::Label('LaunchApplication') -eq 'Открыть приложение') 'one unified app action label'
$root=Join-Path $PSScriptRoot 'UnifiedFixture';New-Item -ItemType Directory -Force $root|Out-Null
try {
 $p=Join-Path $root 'settings.json';$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}')
 Assert ($c.Buttons['Home'].ApplicationPath -eq 'Example.exe') 'legacy opening fallback copied without changing relative path'
 $c.Save($p);$f=[SettingsForm]::new($p)
 $flags=[Reflection.BindingFlags]'NonPublic,Instance';$combo=$f.GetType().GetField('action',$flags).GetValue($f)
 Assert ($combo.Items.Contains('LaunchApplication') -and !$combo.Items.Contains('OpenTargetApplication')) 'settings has exactly one opening choice'
 $panel=$f.GetType().GetField('actionDetails',$flags).GetValue($f)
 Assert ($null -ne (FindCaption $panel 'Какое приложение кнопка должна открыть')) 'exact opening EXE caption in button editor'
 Assert ($null -ne (FindCaption $f 'В каком приложении кнопке разрешено срабатывать')) 'exact scope EXE caption retained separately'
 Assert ($null -ne (FindCaption $f 'Клавиша или сочетание клавиш')) 'exact key caption in main editor'
 Assert (!$panel.AdvancedExpanded) 'advanced options collapsed by default'
 $panel.SetAdvancedExpanded($true)
 $argsBox=$panel.GetType().GetField('arguments',$flags).GetValue($panel)
 Assert ($argsBox.BackColor -eq $panel.BackColor -or $argsBox.BackColor -eq [Drawing.Color]::FromArgb(39,48,64)) 'expanded parameters follow current theme'
 $panel.SetAdvancedExpanded($false)
 $f.SelectButton('Home');$a=[ActionSpec]::new();$a.Action='LaunchApplication';$a.ApplicationPath='Example.exe';$a.Arguments='--profile "Cinema"';$a.WorkingDirectory='.'
 $f.SetEditor($a.Action,'Space','Global',$false,350,150);$f.SetActionDetails($a);Assert ($f.SaveCurrent()) 'real Save unified action';$f.Dispose()
 $saved=[IO.File]::ReadAllText($p);$f=[SettingsForm]::new($p);$f.SelectButton('Home');$r=$f.EditorActionDetails
 Assert ($r.Arguments -eq $a.Arguments -and $r.WorkingDirectory -eq '.' -and $r.ApplicationPath -eq 'Example.exe') 'advanced values survive collapsed reopen'
 Assert ($f.SaveCurrent()) 'Save reopened config';$f.Dispose();Assert ([IO.File]::ReadAllText($p) -eq $saved) 'Save reopen identical JSON'
 $s=[Collections.Generic.List[ActionSpec]]::new();$a.Action='OpenTargetApplication';$s.Add($a);$seq=[SequenceEditorForm]::new($s)
 $combo=$seq.GetType().GetField('action',$flags).GetValue($seq);Assert ($combo.Items.Contains('LaunchApplication') -and !$combo.Items.Contains('OpenTargetApplication') -and $combo.SelectedItem -eq 'LaunchApplication') 'sequence uses unified app choice for legacy token';$edit=$seq.GetType().GetField('edit',$flags).GetValue($seq);Assert ($null -ne (FindCaption $edit 'Какое приложение кнопка должна открыть')) 'exact opening EXE caption in sequence editor';Assert (!$edit.AdvancedExpanded) 'sequence advanced launch remains collapsed';$keyStep=[ActionSpec]::new();$keyStep.Action='Key';$seq.SetStep($keyStep);Assert ($null -ne (FindCaption $edit 'Клавиша или сочетание клавиш')) 'exact key caption in sequence editor';$seq.Dispose()
} finally {if(Test-Path $root){Remove-Item $root -Recurse -Force}}
Write-Output 'ALL UNIFIED APPLICATION TESTS PASSED'
