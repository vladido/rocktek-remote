param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$p=Join-Path $PSScriptRoot 'shortcut-settings.json'
[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save($p)
$f=[SettingsForm]::new($p)
try {
$f.SelectButton('Left');$f.SetEditor('Key','Enter','Global',$false,350,150)
$f.Show();[Windows.Forms.Application]::DoEvents()
Assert ($null -ne $f.GetType().GetMethod('BeginShortcutRecording')) 'explicit shortcut recording exists'
Assert ($f.BeginShortcutRecording()) 'recording starts for Key action'
$m=[Windows.Forms.Message]::Create([IntPtr]::Zero,0x100,[IntPtr]113,[IntPtr]::Zero)
$method=$f.GetType().GetMethod('ProcessCmdKey',[Reflection.BindingFlags]'NonPublic,Instance')
$args=[object[]]@($m,[Windows.Forms.Keys]::F2)
Assert ($method.Invoke($f,$args)) 'actual ProcessCmdKey consumes F2 during recording'
Assert ($f.EditorKey -eq 'F2' -and !$f.RecordingShortcut) 'F2 populates field and finishes recording'
function Command($data){$a=[object[]]@($m,[Windows.Forms.Keys]$data);return $method.Invoke($f,$a)}
function Release($data){$e=[Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]$data);$f.GetType().GetMethod('OnKeyUp',[Reflection.BindingFlags]'NonPublic,Instance').Invoke($f,@($e))|Out-Null}
foreach($pair in @(@([Windows.Forms.Keys]::Space,'Space'),@([Windows.Forms.Keys]::Enter,'Enter'),@([Windows.Forms.Keys]::Back,'Backspace'),@([Windows.Forms.Keys]::PageUp,'PageUp'),@([Windows.Forms.Keys]::PageDown,'PageDown'),@([Windows.Forms.Keys]::D1,'1'),@([Windows.Forms.Keys]::Tab,'Tab'),@(([int][Windows.Forms.Keys]::Control -bor [int][Windows.Forms.Keys]::Right),'Ctrl+Right'),@(([int][Windows.Forms.Keys]::Alt -bor [int][Windows.Forms.Keys]::F4),'Alt+F4'),@(([int][Windows.Forms.Keys]::Control -bor [int][Windows.Forms.Keys]::Alt -bor [int][Windows.Forms.Keys]::Shift -bor [int][Windows.Forms.Keys]::A),'Ctrl+Alt+Shift+A'))){
Release ([Windows.Forms.Keys]::F2)
Assert ($f.BeginShortcutRecording()) ('start recording '+$pair[1])
Assert (Command $pair[0]) ('actual ProcessCmdKey consumes '+$pair[1])
Assert ($f.EditorKey -ceq $pair[1] -and !$f.RecordingShortcut) ('canonical supported token '+$pair[1])
[KeyCombination]::Parse($f.EditorKey)|Out-Null
Release $pair[0]
}
$f.BeginShortcutRecording()|Out-Null
Assert ($f.SaveCurrent() -and !$f.RecordingShortcut) 'Save cancels unfinished recording before persistence'
$bf=[Reflection.BindingFlags]'NonPublic,Instance'
$keyBox=$f.GetType().GetField('key',$bf).GetValue($f)
$recordButton=$f.GetType().GetField('recordButton',$bf).GetValue($f)
Assert ($recordButton.Parent -eq $keyBox.Parent) 'recording control adjacent to manually editable field'
Assert (!$keyBox.ReadOnly) 'manual editing remains available'
$before=$f.EditorKey;$bytes=[IO.File]::ReadAllText($p)
Assert ($f.BeginShortcutRecording()) 'start modifier-only recording'
foreach($modifier in @('ControlKey','ShiftKey','Menu','LWin','RWin')){Assert (Command ([Windows.Forms.Keys]::$modifier)) ('consume standalone '+$modifier);Assert ($f.RecordingShortcut -and $f.EditorKey -eq $before) ('standalone modifier does not finish '+$modifier)}
Assert (Command ([Windows.Forms.Keys]::OemPlus)) 'unsupported key consumed'
Assert ($f.RecordingShortcut -and $f.EditorKey -eq $before) 'unsupported key keeps previous value and waits'
Assert (Command ([Windows.Forms.Keys](([int][Windows.Forms.Keys]::Right) -bor 0x80000))) 'invalid modifier bits consumed'
Assert ($f.RecordingShortcut -and $f.EditorKey -eq $before) 'invalid modifier bits do not record'
Assert (Command ([Windows.Forms.Keys]::Escape)) 'Escape consumed rather than closing'
Assert (!$f.RecordingShortcut -and $f.EditorKey -eq $before -and !$keyBox.ReadOnly) 'Escape restores prior text and manual mode'
Release ([Windows.Forms.Keys]::Escape)
Assert ([IO.File]::ReadAllText($p) -eq $bytes) 'recording and cancellation do not persist automatically'
$f.BeginShortcutRecording()|Out-Null;$recordButton.PerformClick()
Assert (!$f.RecordingShortcut -and $f.EditorKey -eq $before) 'explicit cancel button restores prior text'
$f.BeginShortcutRecording()|Out-Null
$f.GetType().GetMethod('OnDeactivate',$bf).Invoke($f,@([EventArgs]::Empty))|Out-Null
Assert (!$f.RecordingShortcut -and $f.EditorKey -eq $before) 'window focus loss cancels recording'
$f.BeginShortcutRecording()|Out-Null
$f.GetType().GetField('scopeApplication',$bf).GetValue($f).Focus()|Out-Null
# SSH has no active desktop focus; exercise the real control Leave event seam.
$keyBox.GetType().GetMethod('OnLeave',$bf).Invoke($keyBox,@([EventArgs]::Empty))|Out-Null
[Windows.Forms.Application]::DoEvents()
Assert (!$f.RecordingShortcut -and $f.EditorKey -eq $before -and !$keyBox.ReadOnly) 'moving focus to another GUI field cancels recording'
$f.BeginShortcutRecording()|Out-Null
$tabMessage=[Windows.Forms.Message]::Create($keyBox.Handle,0x100,[IntPtr]9,[IntPtr]::Zero)
$activeBefore=$f.ActiveControl
Assert ($keyBox.PreProcessMessage([ref]$tabMessage)) 'actual TextBox preprocessing consumes Tab'
Assert ($f.EditorKey -eq 'Tab' -and !$f.RecordingShortcut -and $f.ActiveControl -eq $activeBefore) 'Tab capture completes without focus navigation'
Release ([Windows.Forms.Keys]::Tab)
$f.BeginShortcutRecording()|Out-Null
$e=[Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Enter)
$f.GetType().GetMethod('OnKeyDown',$bf).Invoke($f,@($e))|Out-Null
Assert ($e.Handled -and $e.SuppressKeyPress -and $f.EditorKey -eq 'Enter') 'real KeyDown fallback consumes canonical Enter'
Release ([Windows.Forms.Keys]::Enter)
Assert (!(Command ([Windows.Forms.Keys]::F2))) 'nonrecording ProcessCmdKey does not intercept ordinary keyboard'
$e=[Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::A)
$f.GetType().GetMethod('OnKeyDown',$bf).Invoke($f,@($e))|Out-Null
Assert (!$e.Handled -and !$e.SuppressKeyPress -and $f.EditorKey -eq 'Enter') 'nonrecording KeyDown does not log or capture ordinary keyboard'
$f.SelectButton('Mute');Assert (!$f.BeginShortcutRecording() -and !$recordButton.Enabled) 'native Mute remains locked'
$f.SelectButton('Left');$f.SetEditor('Disabled','Enter','Global',$false,350,150)
Assert (!$f.BeginShortcutRecording()) 'Disabled action cannot start recording'
$f.SetEditor('Key','Enter','Global',$false,350,150)
$f.BeginShortcutRecording()|Out-Null
Assert ($f.BeginLearning($false) -and !$f.RecordingShortcut -and !$f.BeginShortcutRecording()) 'remote learning and local shortcut recording are separate exclusive modes'
Assert (!(Command ([Windows.Forms.Keys]::F2)) -and !$f.ConfirmLearned('ordinary keyboard')) 'ordinary local key never becomes remote learning signal'
$f.CancelLearning()
$packet=[byte[]](60,0,0,0,0,0,113,0,0,1,0,0,0,0,0,0)
$f.BeginLearning($false)|Out-Null;$f.AcceptLearnInput('AABBCCDDEEFF',[ConsumerInput]::DecodeKeyboard('AABBCCDDEEFF',$packet))|Out-Null
$d=$f.CreateLearnDialog();try{Assert ($d.Text -match 'F2' -and $d.Text -notmatch 'VK|scan|Keyboard') 'friendly learn dialog separates signal from physical name'}finally{$d.Dispose()}
Assert ($f.ConfirmLearned('1')) 'physical digit may be named 1 without F2 suffix'
$f.SetScopeApplicationPath('Example.exe');$f.SetEditor('Key','Space','Application',$true,650,200);$f.BeginShortcutRecording()|Out-Null
Command ([Windows.Forms.Keys](([int][Windows.Forms.Keys]::Control) -bor ([int][Windows.Forms.Keys]::Right)))|Out-Null;Release ([Windows.Forms.Keys]::Right)
Assert ($f.SaveCurrent()) 'recorded shortcut saves through existing atomic JSON path'
$c=[RemoteConfig]::Load($p);$b=$c.Buttons['1']
Assert ($b.Key -eq 'Ctrl+Right' -and $b.InputType -eq 'Keyboard' -and $b.Usage -eq 113 -and $b.ScanCode -eq 60 -and $b.ScanFlags -eq 0 -and $b.Repeat -and $b.Delay -eq 650 -and $b.Interval -eq 200) 'recording preserves source usage scan and repeat metadata'
$f.Dispose();$f=[SettingsForm]::new($p);$f.SelectButton('1');Assert ($f.EditorKey -eq 'Ctrl+Right') 'saved recording reopens from same JSON'
$f.BeginShortcutRecording()|Out-Null;$f.Dispose()
Assert (!$f.RecordingShortcut) 'dispose cleans recording mode'
} finally {$f.Dispose();Remove-Item $p -ErrorAction SilentlyContinue}
'ALL SHORTCUT TESTS PASSED'
