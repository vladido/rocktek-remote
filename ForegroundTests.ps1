param([string]$Assembly)
$ErrorActionPreference='Stop'
$a=[Reflection.Assembly]::LoadFrom($Assembly)
$t=$a.GetType('ForegroundInputJoin')
if(!$t){throw 'FAIL worker input queue cannot join requested foreground activation'}
$calls=New-Object 'Collections.Generic.List[string]'
$join=[Func[uint32,uint32,bool,bool]]{param($x,$y,$on)$calls.Add("$x/$y/$on");return $true}
$activate=[Func[bool]]{return $true}
$r=$t.GetMethod('Run').Invoke($null,@([uint32]1,[uint32]2,[uint32]3,$join,$activate))
if(!$r -or ($calls -join ',') -ne '1/2/True,1/3/True,1/3/False,1/2/False'){throw 'FAIL input join/detach order'}
'PASS foreground and target queue attachments always detached in reverse order'
$calls.Clear()
$bad=[Func[bool]]{throw 'activation fixture failure'}
try{$t.GetMethod('Run').Invoke($null,@([uint32]1,[uint32]2,[uint32]3,$join,$bad))|Out-Null}catch{}
if(($calls -join ',') -ne '1/2/True,1/3/True,1/3/False,1/2/False'){throw 'FAIL exception cleanup'}
'PASS failed activation detaches every owned input join'
$calls.Clear()
$fail=[Func[uint32,uint32,bool,bool]]{param($x,$y,$on)$calls.Add("$x/$y/$on");return !$on}
$r=$t.GetMethod('Run').Invoke($null,@([uint32]1,[uint32]2,[uint32]3,$fail,$activate))
if($r -or $calls.Count -ne 1){throw 'FAIL failed join must not claim activation or detach unowned join'}
'PASS attach failure fails closed'
$m=$t.GetMethod('Retry')
if(!$m){throw 'FAIL activation must retry transient wake refusal'}
$script:tries=0;$script:waits=0
$try=[Func[bool]]{$script:tries++;return $script:tries -ge 3}
$wait=[Action[int]]{param($ms)$script:waits++}
$r=$m.Invoke($null,@($try,$wait))
if(!$r -or $script:tries -ne 3 -or $script:waits -ne 2){throw 'FAIL retry transient foreground refusal'}
'PASS one requested activation retries temporary wake refusal without new input'
$script:tries=0;$script:waits=0
$never=[Func[bool]]{$script:tries++;return $false}
$r=$m.Invoke($null,@($never,$wait))
if($r -or $script:tries -ne 10 -or $script:waits -ne 9){throw 'FAIL foreground retry bounded'}
'PASS permanent activation refusal terminates bounded'

