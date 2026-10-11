param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$previous=$env:ROCKTEK_REMOTE_DEVICE
try{
 foreach($value in @('','invalid','AABBCCDDEEFF00')){
  $env:ROCKTEK_REMOTE_DEVICE=$value
  Assert (![ConsumerInput]::IsTarget('HID_AABBCCDDEEFF')) 'missing or malformed device address fails closed'
 }
 $env:ROCKTEK_REMOTE_DEVICE='AABBCCDDEEFF'
 Assert ([ConsumerInput]::Target -eq 'AABBCCDDEEFF' -and [ConsumerInput]::IsTarget('HID_aabbccddeeff') -and ![ConsumerInput]::IsTarget('HID_001122334455')) 'first selected device accepted exclusively'
 $env:ROCKTEK_REMOTE_DEVICE='001122334455'
 Assert ([ConsumerInput]::IsTarget('HID_001122334455') -and ![ConsumerInput]::IsTarget('HID_AABBCCDDEEFF')) 'alternative configured device replaces fixture identity'
 [UiText]::Initialize([string[]]@('--language','en'));Assert ([UiText]::Get('Открыть настройки') -eq 'Open settings') 'English tray localization preserved'
 [UiText]::Initialize([string[]]@('--language','ru'));Assert ([UiText]::Get('Открыть настройки') -eq 'Открыть настройки') 'Russian tray localization preserved'
}finally{[Environment]::SetEnvironmentVariable('ROCKTEK_REMOTE_DEVICE',$previous,'Process')}
'ALL DEVICE SELECTION AND LOCALIZATION TESTS PASSED'
