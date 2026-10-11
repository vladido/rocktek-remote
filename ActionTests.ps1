param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}');$c.Buttons['Left'].Action='LaunchApplication';$c.Buttons['Left'].Repeat=$false
$c.Buttons['Left'].ApplicationPath='C:\Apps\Example.exe'
try{$c.Validate();$ok=$true}catch{$ok=$false}
Assert $ok 'expanded application action accepted'
Assert (([Reflection.Assembly]::LoadFrom($Assembly)).GetManifestResourceNames() -contains 'app-icon.ico') 'custom application icon embedded resource exists'
[UiText]::Initialize([string[]]@("--language","ru"))
Assert ([RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Version -eq 4) 'generic target application schema4 defaults'
Assert ($null -ne ([SettingsForm].GetMethod('SetActionDetails'))) 'graphical action-specific editor exists'
Assert ($null -ne ([Reflection.Assembly]::LoadFrom($Assembly)).GetType('ActionExecutor')) 'cancellable action executor exists'
[UiText]::Initialize([string[]]@("--language","ru"))
'ALL ACTION TESTS PASSED'
