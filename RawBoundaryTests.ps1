param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
$a=[Reflection.Assembly]::LoadFrom($Assembly)
[UiText]::Initialize([string[]]@("--language","ru"))
$t=$a.GetType('ConsumerInput',$true)
$m=$t.GetMethod('HeaderRejection')
if(!$m){throw 'RED: missing raw header rejection diagnostic seam'}
function Check($type,$size,$got,$hs,$expected){$actual=$m.Invoke($null,@([uint32]$type,[uint32]$size,[uint32]$got,[uint32]$hs));if($actual -cne $expected){throw "Header classification expected '$expected', got '$actual'"}}
Check 1 40 40 24 ''
Check 2 37 37 24 ''
Check 0 40 40 24 'type'
Check 2 38 37 24 'size'
Check 1 41 41 24 'keyboard-size'
Check 1 24 24 24 'short'
$record=$t.GetMethod('RecordIntake');$snapshot=$t.GetMethod('IntakeSnapshot')
if(!$record -or !$snapshot){throw 'Missing intake aggregate seam'}
$before=$snapshot.Invoke($null,@())
$record.Invoke($null,@('name-unavailable'))
$after=$snapshot.Invoke($null,@())
if($after -notmatch 'name-unavailable=1'){throw 'Aggregate rejection counter did not increment'}
try{$record.Invoke($null,@('arbitrary-device-or-key'));throw 'Untrusted metadata accepted'}catch{if($_.Exception.Message -eq 'Untrusted metadata accepted'){throw}}
if($snapshot.Invoke($null,@()) -match 'arbitrary'){throw 'Untrusted metadata leaked'}
'PASS raw boundary classification and aggregate-only rejection diagnostics'
