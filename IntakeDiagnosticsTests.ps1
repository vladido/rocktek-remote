param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
Add-Type -AssemblyName System.Windows.Forms
$form=New-Object System.Windows.Forms.Form
try {
 [ConsumerInput]::Register($form.Handle,$false)
 $snapshot=[ConsumerInput]::RegistrationSnapshot($form.Handle)
 if($snapshot -notmatch 'valid=True registrations=2' -or $snapshot -notmatch 'page=12 usage=1 flags=100' -or $snapshot -notmatch 'page=1 usage=6 flags=100'){throw "Invalid snapshot: $snapshot"}
 if($snapshot -match 'HID#|BTHENUM|Keyboard 0041'){throw 'Snapshot disclosed ordinary-device identity or key content'}
 "PASS read-only native registration snapshot: $snapshot"
}finally{[ConsumerInput]::Register([IntPtr]::Zero,$true);$form.Dispose()}
'ALL NATIVE INTAKE DIAGNOSTIC TESTS PASSED (own-process registration only; no input injection)'
