param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$t=[Reflection.Assembly]::LoadFrom($Assembly).GetType('RockTekRemote')
[UiText]::Initialize([string[]]@("--language","ru"))
$bf=[Reflection.BindingFlags]'NonPublic,Instance,Static'
$o=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($t)
function SetField($name,$value){$t.GetField($name,$bf).SetValue($o,$value)}
function GetField($name){$t.GetField($name,$bf).GetValue($o)}
SetField 'Clock' ([Diagnostics.Stopwatch]::StartNew())
SetField 'HomeLock' (New-Object object)
SetField 'PendingHomes' (New-Object 'Collections.Generic.List[long]')
SetField 'HookLogs' (New-Object 'Collections.Concurrent.ConcurrentQueue[string]')
SetField 'Repeater' (New-Object HoldRepeater)
SetField 'RemoteHomeTick' ([long]-10000)
SetField 'ConfigText' ''
$p=Join-Path $PSScriptRoot 'runtime-settings.json'
SetField 'ConfigPath' $p
SetField 'LogFile' (Join-Path $PSScriptRoot 'runtime-test.log')
try {
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}');$c.Buttons['Left'].Key='Ctrl+Right';$c.Buttons['Left'].Scope='Global';$c.Buttons['Left'].Delay=650;$c.Buttons['Left'].Interval=200;$c.Save($p)
$t.GetMethod('Reload',$bf).Invoke($o,@())|Out-Null
$b=(GetField 'Config').Buttons['Left']
Assert ($b.Key -eq 'Ctrl+Right' -and $b.Scope -eq 'Global' -and $b.Delay -eq 650 -and $b.Interval -eq 200) 'runtime reads exact GUI schema and path'
$c.Buttons['Left'].Key='Enter';$c.Save($p);$t.GetMethod('Reload',$bf).Invoke($o,@())|Out-Null
Assert ((GetField 'Config').Buttons['Left'].Key -eq 'Enter') 'runtime live reload after atomic replacement'
$previous=[IO.File]::ReadAllText($p);[IO.File]::WriteAllText($p,'{bad');$t.GetMethod('Reload',$bf).Invoke($o,@())|Out-Null
Assert ((GetField 'Config').Buttons['Left'].Key -eq 'Enter') 'runtime retains last valid config on invalid edit'
$custom=New-Object ButtonConfig;$custom.Scope='Global';$custom.Usage=181;$c.Buttons.Add('Next',$custom);$c.Save($p);$t.GetMethod('Reload',$bf).Invoke($o,@())|Out-Null
Assert ((GetField 'Config').Find(181).Action -eq 'Disabled') 'runtime live reload sees newly learned usage'
$t.GetMethod('HandleUsage',$bf).Invoke($o,@([string]'AABBCCDDEEFF',[UInt16]181,$false))|Out-Null
Assert ((GetField 'LastUsage') -eq 181) 'dynamic usage reaches actual dispatch boundary without input injection'
$gate=New-Object LearnGate
try {$t.GetMethod('HandleUsage',$bf).Invoke($o,@([string]'AABBCCDDEEFF',[UInt16]68,$false))|Out-Null;Assert ((GetField 'LastUsage') -eq 0 -and (GetField 'Repeater').Due(999999) -eq 0) 'learn lease prevents actual runtime dispatch and repeats'}finally{$gate.Dispose()}
$makeInput=$t.GetMethod('Input',$bf)
$down=$makeInput.Invoke($null,@([UInt16]39,$false));$up=$makeInput.Invoke($null,@([UInt16]39,$true))
Assert ($down.Data.Key.Flags -eq 1 -and $up.Data.Key.Flags -eq 3) 'arrows extended on down and up'
Assert ($down.Data.Key.Extra.ToUInt64() -eq 0x52544B47) 'injection tagged with own extra info'
Assert ([Runtime.InteropServices.Marshal]::SizeOf($down) -eq 40) 'x64 native INPUT size'
$kt=$t.GetNestedType('KeyData',[Reflection.BindingFlags]'NonPublic')
$k=[Activator]::CreateInstance($kt);$k.Vk=0xAC
$ptr=[Runtime.InteropServices.Marshal]::AllocHGlobal([Runtime.InteropServices.Marshal]::SizeOf($k))
try {
foreach($pair in @(@(0x11,0x100),@(0x91,0x101))){$k.Flags=$pair[0];[Runtime.InteropServices.Marshal]::StructureToPtr($k,$ptr,$false);$r=$t.GetMethod('KeyboardHook',$bf).Invoke($o,@(0,[IntPtr]$pair[1],$ptr));Assert ($r -eq [IntPtr]1) ('BrowserHome synthesized flags '+$pair[0]+' suppressed')}
Assert ((GetField 'HookLogs').Count -eq 0) 'global hook does not log unfiltered ordinary keyboard events'
Assert ((GetField 'PendingHomes').Count -eq 1) 'only Home down queued; hook does no player or COM work'
$k.Extra=[UIntPtr]::new([UInt32]0x52544B47);[Runtime.InteropServices.Marshal]::StructureToPtr($k,$ptr,$false);$r=$t.GetMethod('KeyboardHook',$bf).Invoke($o,@(0,[IntPtr]0x100,$ptr));Assert ($r -ne [IntPtr]1) 'own injected BrowserHome passes hook'
} finally {[Runtime.InteropServices.Marshal]::FreeHGlobal($ptr)}
} finally {Remove-Item $p,(Join-Path $PSScriptRoot 'runtime-test.log') -ErrorAction SilentlyContinue}
'ALL RUNTIME TESTS PASSED (no hooks installed, no input injected)'
