param([string]$Assembly = "$PSScriptRoot\RockTekRemote.exe")
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom($Assembly) | Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($ok,$name){if(!$ok){throw "FAIL: $name"};Write-Output "PASS: $name"}
Assert ($null -ne ('RemoteConfig' -as [type])) 'versioned configuration exists'
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}')
Assert ($c.Buttons['OK'].Key -eq 'Space') 'OK default is Space'
Assert ($c.Buttons['Home'].Action -eq 'OpenTargetApplication') 'Home default opens player'
Assert ($c.Buttons['Mute'].Action -eq 'Native') 'Mute remains native'
Assert ($c.Buttons['CH+'].Scope -eq 'Global') 'channel volume global'
Assert ($c.Buttons['Left'].Repeat -and $c.Buttons['Left'].Delay -eq 350 -and $c.Buttons['Left'].Interval -eq 150) 'repeat defaults'
Assert ([KeyCombination]::Parse('Ctrl+Right').Key -eq 39) 'Ctrl+Right parsed'
Assert ([KeyCombination]::Parse('Ctrl+Right').Modifiers[0] -eq 17) 'control parsed'
$bad=$false;try{[KeyCombination]::Parse('Ctrl+Unicorn')}catch{$bad=$true};Assert $bad 'invalid key rejected'
$bad=$false;try{[KeyCombination]::Parse('Ctrl+Ctrl+A')}catch{$bad=$true};Assert $bad 'duplicate modifier rejected'
$c=[RemoteConfig]::Parse('{"MurglarPath":"C:\\Player\\Murglar.exe"}')
Assert ($c.MurglarPath -eq 'C:\Player\Murglar.exe' -and $c.Buttons.Count -eq 10) 'legacy migration'
$bad=$false;try{[RemoteConfig]::Parse('{"Version":99}')}catch{$bad=$true};Assert $bad 'future schema rejected'
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}');$c.Buttons['Left'].Delay=10
$bad=$false;try{$c.Validate()}catch{$bad=$true};Assert $bad 'repeat range rejected'
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}');$c.Buttons['Mute'].Action='Disabled'
$bad=$false;try{$c.Validate()}catch{$bad=$true};Assert $bad 'unsafe mute remap rejected'
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}');$c.Buttons['Home'].Repeat=$true
$bad=$false;try{$c.Validate()}catch{$bad=$true};Assert $bad 'Home repeat rejected'
$p=Join-Path $PSScriptRoot 'test-settings.json';[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save($p)
$c=[RemoteConfig]::Load($p);Assert ($c.Buttons.Count -eq 10) 'atomic round trip'
$c.Buttons['Left'].Key='Ctrl+Right';$c.Save($p);Assert ([RemoteConfig]::Load($p).Buttons['Left'].Key -eq 'Ctrl+Right') 'atomic replacement'
$before=[IO.File]::ReadAllText($p);$c.Buttons['Left'].Key='INVALID'
$bad=$false;try{$c.Save($p)}catch{$bad=$true};Assert ($bad -and [IO.File]::ReadAllText($p) -eq $before) 'invalid save leaves previous file intact'
[IO.File]::WriteAllText($p,'{bad');$errorText='';$loaded=[RemoteConfig]::LoadSafe($p,[ref]$errorText)
Assert ($loaded.Buttons.Count -eq 10 -and $errorText.Length -gt 0 -and [IO.File]::ReadAllText($p) -eq '{bad') 'invalid load defaults without overwrite'
Remove-Item $p
Write-Output 'ALL CONFIG TESTS PASSED'
