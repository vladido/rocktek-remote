param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}')
Assert ($c.Version -eq 4 -and $c.Buttons['Home'].Usage -eq 0x223) 'dynamic schema carries usages'
$b=New-Object ButtonConfig;$b.ScopeApplicationPath='Example.exe';$b.Usage=0xB5
$c.Buttons.Add('Next',$b);$c.Validate()
Assert ($c.Find(0xB5).Action -eq 'Disabled') 'custom default disabled and runtime lookup'
$p=Join-Path $PSScriptRoot 'dynamic-settings.json'
try {
$c.Save($p);$c=[RemoteConfig]::Load($p)
Assert ($c.Buttons.Count -eq 11 -and $c.Find(0xB5).Usage -eq 0xB5) 'dynamic canonical JSON round trip'
$c.Buttons['Next'].Action='Key';$c.Buttons['Next'].Repeat=$true;$c.Validate()
Assert $true 'custom Key repeat allowed'
$c.Buttons['Next'].Usage=0xE2;$bad=$false;try{$c.Validate()}catch{$bad=$true};Assert $bad 'duplicate mute usage rejected'
$c.Buttons['Next'].Usage=0xB5;$c.Buttons['Home'].Usage=0x222;$bad=$false;try{$c.Validate()}catch{$bad=$true};Assert $bad 'default usages immutable'
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}');$old=$c.Json() -replace '"Version":4','"Version":2' -replace '"Usage":\d+,',''
$m=[RemoteConfig]::Parse($old);Assert ($m.Version -eq 4 -and $m.Find(0x223).Action -eq 'OpenTargetApplication') 'schema2 migrates defaults without recapture'
} finally {Remove-Item $p -ErrorAction SilentlyContinue}
'ALL DYNAMIC CONFIG TESTS PASSED'
