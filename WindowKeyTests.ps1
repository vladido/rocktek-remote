param([string]$Assembly)
$ErrorActionPreference='Stop'
$a=[Reflection.Assembly]::LoadFrom($Assembly)
$t=$a.GetType('WindowKeyInput')
if(!$t){throw 'FAIL missing non-waking scoped window-key delivery'}
$m=$t.GetMethod('Messages')
$p=$m.Invoke($null,@('Space',[uint32]0x39))
if($p.Count -ne 2 -or $p[0].Message -ne 0x100 -or $p[0].Vk -ne 32 -or $p[0].LParam -ne 0x390001 -or $p[1].Message -ne 0x101 -or $p[1].LParam -ne [uint32]3224961025){throw 'FAIL Space window make/break payload'}
'PASS Space native window payload without SendInput'
$p=$m.Invoke($null,@('Right',[uint32]0x4d))
if($p[0].LParam -ne 0x14d0001 -or $p[1].LParam -ne [uint32]3243048961){throw 'FAIL extended arrow window make/break payload'}
'PASS extended arrow scan and release bits'
$rejected=$false;try{$m.Invoke($null,@('Ctrl+Right',[uint32]0x4d))|Out-Null}catch{$rejected=$true};if(!$rejected){throw 'FAIL modified window shortcut must not claim unsupported modifier state'}
'PASS unsupported modified shortcut fails closed'
$d=$a.GetType('WindowKeyDelivery').GetMethod('Send')
$script:calls=@();$w=[Action[string]]{param($s)$script:calls+=('window:'+ $s)};$sys=[Action[string]]{param($s)$script:calls+=('system:'+ $s)}
$d.Invoke($null,@($true,'Space',$w,$sys))
if(($script:calls -join ',') -ne 'window:Space'){throw 'FAIL dark scoped delivery used system input'}
$script:calls=@();$d.Invoke($null,@($false,'Space',$w,$sys))
if(($script:calls -join ',') -ne 'system:Space'){throw 'FAIL awake key delivery changed'}
'PASS dark scoped keys avoid system input; awake keys preserved'
