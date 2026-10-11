param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
Assert ($null -ne ('ConsumerInput' -as [type])) 'shared target report decoder exists'
Assert ([ConsumerInput]::Decode('HID#AABBCCDDEEFF',[byte[]](2,181,0,0,0)) -eq 181) 'synthetic target report through actual decoder'
Assert ([ConsumerInput]::Decode('ordinary keyboard',[byte[]](2,181,0,0,0)) -eq -1) 'wrong device ignored'
Assert ([ConsumerInput]::Decode('AABBCCDDEEFF',[byte[]](1,181,0,0,0)) -eq -1) 'wrong report ignored'
Assert ([ConsumerInput]::Decode('AABBCCDDEEFF',[byte[]](2,0,0,0,0)) -eq 0) 'release decoded'
$p=Join-Path $PSScriptRoot 'learn-settings.json';[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save($p)
$f=New-Object SettingsForm($p)
try {
Assert (!$f.AcceptLearnReport('ordinary keyboard',[byte[]](2,181,0,0,0))) 'GUI wrong device rejected'
Assert (!$f.AcceptLearnReport('AABBCCDDEEFF',[byte[]](2,181,0,0,0))) 'GUI ignores reports outside learn'
Assert ($f.BeginLearning($false)) 'learn without native registration for test'
Assert ([LearnGate]::IsLearning()) 'IPC suspends runtime dispatch'
Assert (!$f.BeginLearning($false) -and [LearnGate]::IsLearning()) 'second begin refused without losing existing lease'
Assert (!$f.AcceptLearnReport('AABBCCDDEEFF',[byte[]](2,0,0,0,0))) 'learn ignores release'
Assert ($f.AcceptLearnReport('AABBCCDDEEFF',[byte[]](2,181,0,0,0))) 'learn captures one target down'
Assert ($f.LearnedDescription -ceq 'Мультимедиа · код 0x00B5') 'learned Consumer description includes exact signal code'
Assert (!$f.AcceptLearnReport('AABBCCDDEEFF',[byte[]](2,181,0,0,0))) 'held duplicate ignored'
Assert ([RemoteConfig]::Load($p).Buttons.Count -eq 10) 'capture not persisted before confirmation'
Assert ($f.ConfirmLearned('Next')) 'name confirmation adds disabled custom'
Assert (![LearnGate]::IsLearning()) 'IPC released after confirmation'
$f.SelectButton('Next');$f.SetScopeApplicationPath('Example.exe');$f.SetEditor('Key','Ctrl+Right','Application',$true,650,200)
Assert ($f.SaveCurrent()) 'custom GUI save';$c=[RemoteConfig]::Load($p)
Assert ($c.Find(181).Key -eq 'Ctrl+Right' -and $c.Find(181).Repeat) 'runtime custom lookup after GUI save'
$f.Dispose();$f=New-Object SettingsForm($p);$f.SelectButton('Next');Assert ($f.EditorKey -eq 'Ctrl+Right') 'custom editor reopens'
Assert ($f.BeginLearning($false)) 'learn existing button'
Assert ($f.AcceptLearnReport('AABBCCDDEEFF',[byte[]](2,226,0,0,0))) 'native mute selects existing'
Assert (!$f.ConfirmLearned('OtherMute')) 'duplicate known usage does not add'
Assert (!$f.RemoveButton('Mute')) 'default removal blocked'
Assert ($f.RemoveButton('Next')) 'custom removal allowed'
Assert ($f.BeginLearning($false)) 'cancel begin';$f.CancelLearning();Assert (![LearnGate]::IsLearning()) 'cancel releases IPC'
Assert ($f.BeginLearning($false)) 'timeout begin';$f.ExpireLearning();Assert (![LearnGate]::IsLearning()) 'timeout releases IPC'
Assert ($f.BeginLearning($true)) 'real Consumer and Keyboard Raw Input registration';$f.CancelLearning();Assert (![LearnGate]::IsLearning()) 'native unregister restores lease'
Assert ($f.BeginLearning($false)) 'dispose begin';$f.Dispose();Assert (![LearnGate]::IsLearning()) 'dispose releases IPC'
} finally {$f.Dispose();Remove-Item $p -ErrorAction SilentlyContinue}
'ALL LEARN TESTS PASSED (synthetic reports, no physical capture claim)'
