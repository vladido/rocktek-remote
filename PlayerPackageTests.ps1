param([string]$Stage=$PSScriptRoot)
$ErrorActionPreference='Stop'
function Assert($value,$message){if(!$value){throw ('FAIL '+$message)}; 'PASS '+$message}
$dist=Join-Path $Stage 'dist'
[Reflection.Assembly]::LoadFrom((Join-Path $dist 'RockTekRemote.exe'))|Out-Null
foreach($player in @('CloudMus','Murglar')){
 $file=Join-Path $dist ('profiles\'+$player+'.json')
 Assert (Test-Path $file) ($player+' default profile is packaged')
 $config=[RemoteConfig]::Load($file)
 $openAction=$config.Buttons['Home'];$ok=$config.Buttons['OK']
 Assert ($openAction.Action -eq 'LaunchApplication' -and $openAction.Arguments.StartsWith('--open ') -and !$openAction.Repeat -and $openAction.Scope -eq 'Global') ($player+' native Home default')
 Assert ($ok.Action -eq 'LaunchApplication' -and !$ok.Repeat -and $ok.Scope -eq 'Application') ($player+' scoped native OK default')
 $helper=[ActionValidation]::ResolvePath($openAction.ApplicationPath,$file)
 Assert (Test-Path $helper) ($player+' helper resolves relative to JSON, not current directory')
 Assert ($openAction.Arguments.Contains('--application "'+$ok.ScopeApplicationPath+'"') -and $ok.Arguments.Contains('--application "'+$ok.ScopeApplicationPath+'"')) ($player+' exact configured target passed to bridge')
 Assert ($config.Buttons['Mute'].Action -eq 'Native' -and $config.Find(48).Action -eq 'Disabled') ($player+' native mute and no default shutdown')
 if($player -eq 'Murglar'){
  foreach($usage in @(554,150)){$rating=$config.Find($usage);Assert ($rating.Scope -eq 'Application' -and !$rating.Repeat -and $rating.ApplicationPath -eq $ok.ApplicationPath) ('Murglar rating guard '+$usage)}
  Assert ($config.Find(554).Arguments.StartsWith('--like ') -and $config.Find(150).Arguments.StartsWith('--dislike ')) 'heart toggle and track-only dislike defaults'
 }else{Assert ($config.Find(554).Key -eq 'L' -and $config.Find(150).Key -eq 'D') 'CloudMus native rating shortcuts'}
}
'ALL PLAYER PACKAGE TESTS PASSED'
