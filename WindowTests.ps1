param([string]$Assembly="$PSScriptRoot\dist\RockTekRemote.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Reflection.Assembly]::LoadFrom($Assembly)|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
function Assert($v,$s){if(!$v){throw "FAIL $s"};"PASS $s"}
Assert ($null -ne ('WindowLayout' -as [type])) 'DPI-aware working-area sizing exists'
[WindowLayout]::InitializeDpi()|Out-Null
foreach($size in @(@(320,240),@(1280,680),@(3840,2040))){$work=[Drawing.Rectangle]::new(100,50,$size[0],$size[1]);$fit=[WindowLayout]::Fit($work,[Drawing.Size]::new(3060,1800),24);Assert ($work.Contains($fit) -and $fit.Bottom -le $work.Bottom) ('fit stays inside small/scaled work area '+$size[0]+'x'+$size[1])}
$p=Join-Path $PSScriptRoot 'window-settings.json';[RemoteConfig]::Parse('{"MurglarPath":"Example.exe"}').Save($p)
$f=[SettingsForm]::new($p)
try {
$f.Show();[Windows.Forms.Application]::DoEvents();$work=[Windows.Forms.Screen]::FromHandle($f.Handle).WorkingArea
Assert ($work.Contains($f.Bounds) -and $f.Bottom -le $work.Bottom) 'actual DPI-aware editor is inside current taskbar working area'
$buttons=New-Object 'Collections.Generic.List[Windows.Forms.Button]'
function CollectButtons($c){if($c -is [Windows.Forms.Button]){$buttons.Add($c)};foreach($child in $c.Controls){CollectButtons $child}}
CollectButtons $f
foreach($button in $buttons){Assert ($button.Parent.ClientRectangle.Contains($button.Bounds)) ('important button fully visible: '+$button.Text)}
Assert ($f.MinimumSize.Width -le $work.Width -and $f.MinimumSize.Height -le $work.Height) 'minimum size never exceeds current work area'
Assert ($f.BeginLearning($false) -and $f.AcceptLearnReport('AABBCCDDEEFF',[byte[]](2,181,0,0,0))) 'learning fixture for name dialog'
$d=$f.CreateLearnDialog()
try {$d.Show();[Windows.Forms.Application]::DoEvents();$work=[Windows.Forms.Screen]::FromHandle($d.Handle).WorkingArea;Assert ($work.Contains($d.Bounds)) 'actual learning name dialog stays above taskbar';$d.Close()}finally{$d.Dispose()}
$f.CancelLearning()
[pscustomobject]@{DpiMode=[WindowLayout]::DpiMode;EditorBounds=$f.Bounds.ToString();WorkingArea=$work.ToString();Minimum=$f.MinimumSize.ToString()}|ConvertTo-Json -Compress
}finally{$f.Dispose();Remove-Item $p -ErrorAction SilentlyContinue}
$fallbackCode=@'
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Reflection.Assembly]::LoadFrom('__ASSEMBLY__')|Out-Null
[UiText]::Initialize([string[]]@("--language","ru"))
[WindowLayout].GetMethod('SystemFallback',[Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@())|Out-Null
if([WindowLayout]::DpiMode -ne 'SystemAware fallback'){throw 'Native DPI fallback did not initialize'}
$f=[Windows.Forms.Form]::new();$f.Size=[Drawing.Size]::new(980,600)
try{[WindowLayout]::ScaleAndFit($f,$null);$w=[Windows.Forms.Screen]::FromHandle($f.Handle).WorkingArea;if(!$w.Contains($f.Bounds)){throw 'Fallback outside working area'};'PASS live native SystemAware fallback API and working-area fit'}finally{$f.Dispose()}
'@
$fallbackCode=$fallbackCode.Replace('__ASSEMBLY__',($Assembly.Replace("'","''")))
$encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($fallbackCode))
& powershell.exe -NoProfile -NonInteractive -EncodedCommand $encoded
Assert ($LASTEXITCODE -eq 0) 'isolated native fallback child succeeded'
'ALL WINDOW TESTS PASSED (current host DPI; no display/taskbar changes)'
