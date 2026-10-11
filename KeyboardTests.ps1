param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$a=[Reflection.Assembly]::LoadFrom($Assembly)
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
Assert ($null -ne [ButtonConfig].GetProperty('InputType')) 'button identity includes Keyboard or Consumer source'
$dev='HID#AABBCCDDEEFF'
$make=[byte[]](60,0,0,0,0,0,113,0,0,1,0,0,0,0,0,0)
$break=$make.Clone();$break[2]=1;$break[8]=1
$e=[ConsumerInput]::DecodeKeyboard($dev,$make)
Assert ($e.InputType -eq 'Keyboard' -and $e.Code -eq 113 -and !$e.IsRelease -and $e.ScanCode -eq 60) 'actual numeric1 F2 make bytes decoded'
Assert ([ConsumerInput]::DecodeKeyboard($dev,$break).IsRelease) 'actual F2 break decoded'
Assert ($null -eq [ConsumerInput]::DecodeKeyboard('ordinary keyboard',$make)) 'ordinary keyboard excluded before callback'
Assert ($null -eq [ConsumerInput]::DecodeKeyboard($dev,[byte[]](1,2))) 'truncated keyboard rejected'
$invalid=$make.Clone();$invalid[6]=255;Assert ($null -eq [ConsumerInput]::DecodeKeyboard($dev,$invalid)) 'keyboard overrun rejected'
$events=New-Object 'Collections.Generic.List[RemoteInputEvent]'
$cb=[Action[string,RemoteInputEvent]]{param($d,$ev)$events.Add($ev)}
[ConsumerInput]::ReadPacket($dev,1,$make,$cb)
[ConsumerInput]::ReadPacket('ordinary keyboard',1,$make,$cb)
[ConsumerInput]::ReadPacket($dev,1,$break,$cb)
Assert ($events.Count -eq 2 -and $events[0].Code -eq 113 -and $events[1].IsRelease) 'type1 packet parser reaches shared callback only for target'
$extended=$make.Clone();$extended[2]=6
Assert ([ConsumerInput]::DecodeKeyboard($dev,$extended).ScanFlags -eq 6) 'E0 E1 scan flags retained as metadata'
$payload=[byte[]](5,0,0,0,2,0,0,0,2,181,0,0,0,2,0,0,0,0)
[ConsumerInput]::ReadPacket($dev,2,$payload,$cb)
Assert ($events.Count -eq 4 -and $events[2].InputType -eq 'Consumer' -and $events[3].IsRelease) 'consumer multi-report callback and zero release preserved'
[ConsumerInput]::ReadPacket($dev,2,[byte[]](5,0,0,0,255,255,255,127),$cb)
Assert ($events.Count -eq 4) 'oversized consumer count rejected without overflow'
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}');$b=New-Object ButtonConfig;$b.ScopeApplicationPath='Example.exe';$b.InputType='Keyboard';$b.Usage=65;$c.Buttons.Add('KeyboardA',$b);$c.Validate()
Assert ($c.FindInput('Keyboard',65) -eq $b -and $c.Find(65) -eq $c.Buttons['OK']) 'same numeric code in different sources is not collision'
$old=$c.Json() -replace ',?"InputType":"Consumer",?',','
# Legacy fixture constructed without the new optional property.
$legacy=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Json() -replace '"InputType":"Consumer",',''
Assert ([RemoteConfig]::Parse($legacy).Buttons['Home'].InputType -eq 'Consumer') 'old v3 defaults to Consumer without usage changes'
$b2=New-Object ButtonConfig;$b2.InputType='Keyboard';$b2.Usage=65;$c.Buttons.Add('DuplicateA',$b2)
$bad=$false;try{$c.Validate()}catch{$bad=$true};Assert $bad 'same source-code duplicate rejected'
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}');$c.Buttons['Mute'].InputType='Keyboard';$bad=$false;try{$c.Validate()}catch{$bad=$true};Assert $bad 'native defaults cannot change source'
$p=Join-Path $PSScriptRoot 'keyboard-settings.json';[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save($p)
$f=New-Object SettingsForm($p)
try {
Assert ($f.BeginLearning($false)) 'keyboard learning started'
Assert (!$f.AcceptLearnInput($dev,[ConsumerInput]::DecodeKeyboard($dev,$break))) 'learning ignores keyboard break'
Assert ($f.AcceptLearnInput($dev,$e)) 'learning captures target F2 make'
Assert ($f.LearnedDescription -eq 'Клавиатура · F2 · код 0x0071') 'learning shows understandable remote signal and actual F2 separately from physical button name'
Assert ($f.ConfirmLearned('Numeric1')) 'keyboard learned entry confirmed'
$f.SelectButton('Numeric1');$f.SetEditor('Disabled','Enter','Global',$false,350,150)
Assert ($f.SaveCurrent()) 'keyboard GUI actual Save'
$c=[RemoteConfig]::Load($p);$b=$c.FindInput('Keyboard',113)
Assert ($b.InputType -eq 'Keyboard' -and $b.ScanCode -eq 60 -and $b.Key -eq 'Enter') 'StoreEditor preserves keyboard source and scan metadata'
$f.Dispose();$f=New-Object SettingsForm($p)
$beforeSelection=[IO.File]::ReadAllText($p)
$memoryField=$f.GetType().GetField('config',[Reflection.BindingFlags]'NonPublic,Instance');$beforeMemory=$memoryField.GetValue($f).Json()
$f.SelectButton('Numeric1');Assert ($f.EditorKey -eq 'Enter') 'keyboard GUI reopen'
$inputLabel=$f.GetType().GetField('inputInfo',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($f)
Assert ($inputLabel.Text -ceq 'Клавиатура · F2 · код 0x0071') 'SelectButton learned F2 displays actual saved VK code'
Assert ([IO.File]::ReadAllText($p) -ceq $beforeSelection -and $memoryField.GetValue($f).Json() -ceq $beforeMemory) 'selecting learned F2 preserves file and all mappings'
Assert ($f.BeginLearning($false) -and $f.AcceptLearnInput($dev,$e) -and !$f.ConfirmLearned('DuplicateF2')) 'keyboard duplicate selects existing'
# Exercise runtime without hooks, input injection, production config or constructor.
$t=$a.GetType('RockTekRemote');$bf=[Reflection.BindingFlags]'NonPublic,Instance,Static'
$o=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($t)
function SetField($n,$v){$t.GetField($n,$bf).SetValue($o,$v)}
function GetField($n){$t.GetField($n,$bf).GetValue($o)}
SetField 'Clock' ([Diagnostics.Stopwatch]::StartNew());SetField 'Repeater' (New-Object HoldRepeater);SetField 'Config' $c
$handle=$t.GetMethod('HandleInput',$bf)
$handle.Invoke($o,@([string]$dev,$e,$false))|Out-Null
Assert ((GetField 'LastUsage') -eq 113 -and (GetField 'LastSource') -eq 'Keyboard') 'target Keyboard reaches actual runtime dispatch'
$handle.Invoke($o,@([string]'ordinary keyboard',$e,$false))|Out-Null
Assert ((GetField 'LastUsage') -eq 113) 'ordinary keyboard does not disturb target state'
$other=$make.Clone();$other[6]=114;$other[2]=1
$handle.Invoke($o,@([string]$dev,[ConsumerInput]::DecodeKeyboard($dev,$other),$false))|Out-Null
Assert ((GetField 'LastUsage') -eq 113) 'wrong keyboard keyup does not release held F2'
$consumerRelease=New-Object RemoteInputEvent;$consumerRelease.InputType='Consumer';$consumerRelease.Code=0;$consumerRelease.IsRelease=$true
$handle.Invoke($o,@([string]$dev,[RemoteInputEvent]$consumerRelease,$false))|Out-Null
Assert ((GetField 'LastUsage') -eq 113) 'Consumer release does not release Keyboard hold'
$handle.Invoke($o,@([string]$dev,[ConsumerInput]::DecodeKeyboard($dev,$break),$false))|Out-Null
Assert ((GetField 'LastUsage') -eq 0) 'matching F2 break releases runtime hold'
# Direct Disabled fixture enables scheduler testing without any SendInput.
$b.Repeat=$true
$handle.Invoke($o,@([string]$dev,$e,$false))|Out-Null
$r=GetField 'Repeater';$started=$r.GetType().GetField('Started',$bf).GetValue($r)
Start-Sleep -Milliseconds 10
$handle.Invoke($o,@([string]$dev,$e,$false))|Out-Null
Assert ($r.GetType().GetField('Started',$bf).GetValue($r) -eq $started) 'native keyboard autorepeat does not reset scheduled hold'
$handle.Invoke($o,@([string]$dev,[ConsumerInput]::DecodeKeyboard($dev,$break),$false))|Out-Null
Assert ($r.Due(1000) -eq 0) 'keyboard break stops repeats'
$c.Buttons['OK'].Action='Disabled'
$kb=New-Object ButtonConfig;$kb.Scope='Global';$kb.InputType='Keyboard';$kb.Usage=65;$c.Buttons.Add('KeyboardA',$kb)
$ka=New-Object RemoteInputEvent;$ka.InputType='Keyboard';$ka.Code=65
$ca=New-Object RemoteInputEvent;$ca.InputType='Consumer';$ca.Code=65
$handle.Invoke($o,@([string]$dev,[RemoteInputEvent]$ka,$false))|Out-Null
$started=$r.GetType().GetField('Started',$bf).GetValue($r);Start-Sleep -Milliseconds 10
$handle.Invoke($o,@([string]$dev,[RemoteInputEvent]$ca,$false))|Out-Null
Assert ((GetField 'LastSource') -eq 'Consumer' -and $r.GetType().GetField('Started',$bf).GetValue($r) -gt $started) 'same-code source switch dispatches and resets hold context'
$ka.IsRelease=$true;$handle.Invoke($o,@([string]$dev,[RemoteInputEvent]$ka,$false))|Out-Null
Assert ((GetField 'LastSource') -eq 'Consumer') 'Keyboard keyup does not clear same-code Consumer'
$handle.Invoke($o,@([string]$dev,[RemoteInputEvent]$consumerRelease,$false))|Out-Null
Assert ((GetField 'LastUsage') -eq 0) 'Consumer zero clears Consumer hold'

}finally{$f.Dispose();Remove-Item $p -ErrorAction SilentlyContinue}
'ALL KEYBOARD TESTS PASSED (synthetic packets; physical validation required)'
