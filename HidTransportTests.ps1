param([string]$Assembly)
$ErrorActionPreference='Stop'
$a=[Reflection.Assembly]::LoadFrom($Assembly)
$t=$a.GetType('HidConsumerReader')
if(!$t){throw 'FAIL direct Consumer transport missing'}
$r=[Activator]::CreateInstance($t,@($false))
$take=$t.GetMethod('Take');$offer=$t.GetMethod('Offer');$out=$null
function Assert($c,$s){if(!$c){throw ('FAIL '+$s)};Write-Output ('PASS '+$s)}
$report=[byte[]](2,0x9c,0,0,0)
Assert (!$offer.Invoke($r,@('other',[byte[]]$report))) 'unrelated device rejected'
$device='hid#AABBCCDDEEFF&col02#fixture'
Assert ($offer.Invoke($r,@($device,[byte[]]$report))) 'target CH accepted without Raw Input'
$item=$take.Invoke($r,@());Assert ($item.Input.Code -eq 0x9c -and $item.Input.InputType -eq 'Consumer' -and !$item.Input.IsRelease) 'CH decoded'
Assert ($offer.Invoke($r,@($device,[byte[]](2,0,0,0,0)))) 'release accepted'
Assert ($take.Invoke($r,@()).Input.IsRelease) 'release decoded'
Assert (!$offer.Invoke($r,@($device,[byte[]](3,0x9c,0,0,0)))) 'wrong report rejected'
Assert (!$offer.Invoke($r,@('hid#AABBCCDDEEFF&col01#fixture',[byte[]]$report))) 'keyboard collection rejected'
for($i=0;$i -lt 300;$i++){$null=$offer.Invoke($r,@($device,[byte[]]$report))}
Assert ($r.Pending -le 128) 'bounded queue'
$r.Dispose();Assert (!$offer.Invoke($r,@($device,[byte[]]$report))) 'disposed reader rejects reports'
