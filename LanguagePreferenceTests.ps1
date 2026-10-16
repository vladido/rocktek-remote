param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
$fixture=Join-Path $PSScriptRoot 'ui-language-test'
New-Item -ItemType Directory -Force $fixture|Out-Null
$pref=Join-Path $fixture 'language.txt'
$culture=[Threading.Thread]::CurrentThread.CurrentUICulture
try {
 [Threading.Thread]::CurrentThread.CurrentUICulture=[Globalization.CultureInfo]::GetCultureInfo('ru-RU')
 [UiText]::Initialize([string[]]@(),$pref)
 Assert ([UiText]::Language -eq 'ru') 'first launch follows Russian Windows UI language'
 [UiText]::SelectLanguage('en')
 Assert ([IO.File]::ReadAllText($pref).Trim() -eq 'en') 'GUI language choice is persisted separately from mappings'
 [UiText]::Language='ru'
 [UiText]::Initialize([string[]]@(),$pref)
 Assert ([UiText]::Language -eq 'en') 'language survives reopening without a command-line override'
 [UiText]::Initialize([string[]]@('--language','ru'),$pref)
 Assert ([UiText]::Language -eq 'ru') 'explicit language override remains available'
 Assert ([IO.File]::ReadAllText($pref).Trim() -eq 'en') 'command-line override does not replace the saved preference'
 [IO.File]::WriteAllText($pref,'unsupported')
 [UiText]::Initialize([string[]]@(),$pref)
 Assert ([UiText]::Language -eq 'ru') 'invalid saved preference falls back to Windows UI culture'
} finally {
 [Threading.Thread]::CurrentThread.CurrentUICulture=$culture
 Remove-Item $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
'ALL LANGUAGE PREFERENCE TESTS PASSED'
