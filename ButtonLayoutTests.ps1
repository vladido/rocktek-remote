param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
[WindowLayout]::InitializeDpi();[Windows.Forms.Application]::EnableVisualStyles()
function Assert($value,$message){if(!$value){throw "FAIL $message"};"PASS $message"}
function FindButtons($control){if($control -is [Windows.Forms.Button] -and $control.Visible){$control};foreach($child in $control.Controls){FindButtons $child}}
function AssertButtonLayout($form){
 $all=@(FindButtons $form);$browse=@($all|Where-Object Text -eq 'Обзор…')
 Assert ($browse.Count -eq 2) 'both independent EXE Browse buttons visible'
 Assert ($browse[0].Size -eq $browse[1].Size) ('Browse equal width/height: '+$browse[0].Size+' / '+$browse[1].Size)
 $bounds=@($browse|ForEach-Object {$_.Parent.RectangleToScreen($_.Bounds)})
 Assert ($bounds[0].Right -eq $bounds[1].Right) 'Browse aligned at same right edge'
 $exe=@($browse|ForEach-Object {@($_.Parent.Controls|Where-Object {$_ -is [Windows.Forms.TextBox]})[0]})
 $rects=@($exe|ForEach-Object {$_.Parent.RectangleToScreen($_.Bounds)})
 Assert ($rects[0].Left -eq $rects[1].Left -and $rects[0].Right -eq $rects[1].Right -and $exe[0].Height -eq $exe[1].Height) ('EXE inputs equal Left/Right/Height: '+$rects[0]+' / '+$rects[1])
 $flags=[Reflection.BindingFlags]'Instance,NonPublic';$action=$form.GetType().GetField('action',$flags).GetValue($form);$scope=$form.GetType().GetField('scope',$flags).GetValue($form)
 foreach($combo in @($action,$scope)){$r=$combo.Parent.RectangleToScreen($combo.Bounds);Assert ($r.Left -eq $rects[0].Left) 'EXE and combo fields share left column';Assert ($r.Right -eq $bounds[0].Right) 'combo and composite rows share right edge'}
 foreach($button in $browse){
  $text=@($button.Parent.Controls|Where-Object {$_ -is [Windows.Forms.TextBox]})[0]
  $b=$button.Parent.RectangleToScreen($button.Bounds);$t=$text.Parent.RectangleToScreen($text.Bounds)
  Assert ([Math]::Abs(($b.Top+$b.Bottom)-($t.Top+$t.Bottom)) -le 2) 'Browse vertically centered beside EXE input'
  Assert ($button.Parent.ClientRectangle.Contains($button.Bounds)) 'Browse within picker bounds'
 }
 foreach($titles in @(@('Изменить…','По умолчанию'),@('Добавить кнопку','Удалить кнопку'),@('Сохранить','Сбросить','Импорт…','Экспорт…','Закрыть'))){
  $group=@($all|Where-Object {$titles -contains $_.Text});Assert ($group.Count -eq $titles.Count) ('group present: '+($titles -join ', '))
  foreach($button in $group){Assert ($button.Size -eq $group[0].Size) ('consistent group size: '+$button.Text);Assert ($button.Parent.ClientRectangle.Contains($button.Bounds)) ('group button unclipped: '+$button.Text)}
 }
 [pscustomobject]@{Scale=[WindowLayout]::ScaleFor($form);Browse=@($browse|ForEach-Object {[pscustomobject]@{Size=$_.Size.ToString();Screen=$_.RectangleToScreen($_.ClientRectangle).ToString()}})}|ConvertTo-Json -Depth 5 -Compress
}
function AssertSequenceFields($form){
 $flags=[Reflection.BindingFlags]'Instance,NonPublic';$panel=$form.GetType().GetField('edit',$flags).GetValue($form)
 $app=$panel.GetType().GetField('application',$flags).GetValue($panel);$dir=$panel.GetType().GetField('directory',$flags).GetValue($panel);$argsBox=$panel.GetType().GetField('arguments',$flags).GetValue($panel)
 $a=$app.Parent.RectangleToScreen($app.Bounds);$d=$dir.Parent.RectangleToScreen($dir.Bounds);$arg=$argsBox.Parent.RectangleToScreen($argsBox.Bounds)
 Assert ($a.Left -eq $d.Left -and $a.Right -eq $d.Right -and $a.Height -eq $d.Height -and $a.Left -eq $arg.Left) 'sequence EXE/directory/arguments share input column and picker geometry'
 $b=@(FindButtons $panel|Where-Object Text -eq 'Обзор…');Assert ($b.Count -eq 2 -and $b[0].Size -eq $b[1].Size) 'sequence picker buttons identical'
 [pscustomobject]@{Application=$a.ToString();Directory=$d.ToString();Arguments=$arg.ToString();BrowseSize=$b[0].Size.ToString()}|ConvertTo-Json -Compress
}
# An explicit, disposable config: never runtime, global hooks or production Save.
$p=Join-Path $PSScriptRoot 'button-layout-settings.json'
$c=[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}');$c.Save($p);$before=[IO.File]::ReadAllBytes($p)
$f=[SettingsForm]::new($p)
try{$f.SetEditor('LaunchApplication','Space','Application',$false,350,150);$f.SetScopeApplicationPath('ScopeExample.exe');$f.Show();[Windows.Forms.Application]::DoEvents();AssertButtonLayout $f;Assert ([Convert]::ToBase64String($before) -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes($p))) 'layout leaves settings bytes unchanged'}finally{$f.Dispose();Remove-Item $p -ErrorAction SilentlyContinue}
'ALL BUTTON LAYOUT TESTS PASSED'
