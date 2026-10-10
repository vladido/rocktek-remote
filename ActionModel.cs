using System;
using System.IO;
using System.Collections.Generic;
public class ActionSpec {
 public string Action {get;set;} public string Key {get;set;}
 public string ApplicationPath {get;set;} public string Arguments {get;set;} public string WorkingDirectory {get;set;} public string Target {get;set;}
 public string MouseAction {get;set;} public int ScrollAmount {get;set;} public int WaitMilliseconds {get;set;} public List<ActionSpec> Steps {get;set;}
 public ActionSpec(){Action="Disabled";Key="Space";ApplicationPath="";Arguments="";WorkingDirectory="";Target="";MouseAction="LeftClick";ScrollAmount=1;}
}
public static class ActionValidation {
 public static string ResolvePath(string value,string settings){
  string p=Environment.ExpandEnvironmentVariables(value??"");
  if(String.IsNullOrWhiteSpace(p)||p.IndexOf('\0')>=0||p.StartsWith("\\")||p.StartsWith("/")||(p.Length>2&&p.IndexOf(':',2)>=0))throw new ArgumentException(UiText.Get("Укажите обычный локальный путь без сетевых и служебных адресов."));
  if(p.IndexOf(':')>=0&&!(p.Length>=3&&Char.IsLetter(p[0])&&p[1]==':'&&(p[2]=='\\'||p[2]=='/')))throw new ArgumentException(UiText.Get("Путь диска должен быть абсолютным."));
  string full=Path.GetFullPath(Path.IsPathRooted(p)?p:Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settings)),p));
  if(full.StartsWith("\\")||full.Length<3||full[1]!=':')throw new ArgumentException(UiText.Get("Нужен локальный путь диска."));foreach(string segment in full.Substring(3).Split(new[]{'\\','/'})){string stem=segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();if(stem=="CON"||stem=="PRN"||stem=="AUX"||stem=="NUL"||stem=="CONIN$"||stem=="CONOUT$"||((stem.StartsWith("COM")||stem.StartsWith("LPT"))&&stem.Length==4&&(Char.IsDigit(stem[3])||"¹²³".IndexOf(stem[3])>=0)))throw new ArgumentException(UiText.Get("Служебные имена устройств запрещены."));}return full;
 }
 static readonly HashSet<string> ExecutableExtensions=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".exe",".com",".bat",".cmd",".ps1",".psm1",".psd1",".py",".pyw",".rb",".rbw",".pl",".pm",".php",".sh",".bash",".lua",".tcl",".au3",".ahk",".jar",".class",".dll",".sys",".inf",".scf",".sct",".settingcontent-ms",".vbs",".vbe",".js",".jse",".wsf",".wsh",".hta",".scr",".pif",".lnk",".url",".msi",".msp",".msc",".cpl",".reg",".application",".appref-ms"};
 public static string OpenTarget(string value,string settings){
  string v=Environment.ExpandEnvironmentVariables(value??"");Uri uri;
  if(Uri.TryCreate(v,UriKind.Absolute,out uri)&&!uri.IsFile){if(uri.Scheme!="http"&&uri.Scheme!="https")throw new ArgumentException(UiText.Get("Для ссылки разрешены только http и https."));if(String.IsNullOrEmpty(uri.Host)||v.IndexOf('\0')>=0)throw new ArgumentException(UiText.Get("Некорректная ссылка."));return v;}
  string p=ResolvePath(v,settings);if(ExecutableExtensions.Contains(Path.GetExtension(p)))throw new ArgumentException(UiText.Get("Программы запускайте действием Открыть приложение; исполняемые файлы и сценарии здесь запрещены."));return p;
 }
 public static void Relocate(ActionSpec a,string oldSettings){if(a.Action=="LaunchApplication"||a.Action=="OpenTargetApplication"&&!String.IsNullOrWhiteSpace(a.ApplicationPath)){a.ApplicationPath=ResolvePath(a.ApplicationPath,oldSettings);if(!String.IsNullOrWhiteSpace(a.WorkingDirectory))a.WorkingDirectory=ResolvePath(a.WorkingDirectory,oldSettings);}else if(a.Action=="OpenTarget")a.Target=OpenTarget(a.Target,oldSettings);else if(a.Action=="Sequence"&&a.Steps!=null)foreach(var s in a.Steps)Relocate(s,oldSettings);}
 public static void Validate(ActionSpec a,bool step){
  if(a==null)throw new ArgumentException(UiText.Get("Пустое действие."));
  switch(a.Action){
   case "Disabled":case "Native":if(step)throw new ArgumentException(UiText.Get("Недопустимый шаг последовательности."));break;
   case "Key":KeyCombination.Parse(a.Key);break;
   case "OpenTargetApplication":if(String.IsNullOrWhiteSpace(a.ApplicationPath)&&!step)break;goto case "LaunchApplication";case "LaunchApplication":string exe=ResolvePath(a.ApplicationPath,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"settings.json"));if(!String.Equals(Path.GetExtension(exe),".exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException(UiText.Get("Выберите приложение .exe."));if((a.Arguments??"").Length>8192||(a.Arguments??"").IndexOf('\0')>=0)throw new ArgumentException(UiText.Get("Некорректные аргументы."));if(!String.IsNullOrWhiteSpace(a.WorkingDirectory))ResolvePath(a.WorkingDirectory,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"settings.json"));break;
   case "OpenTarget":OpenTarget(a.Target,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"settings.json"));break;
   case "Mouse":if(a.MouseAction!="LeftClick"&&a.MouseAction!="RightClick"&&a.MouseAction!="DoubleClick"&&a.MouseAction!="Scroll")throw new ArgumentException(UiText.Get("Неизвестное действие мыши."));if(a.ScrollAmount==0||a.ScrollAmount< -100||a.ScrollAmount>100)throw new ArgumentException(UiText.Get("Прокрутка: от −100 до 100, кроме нуля."));break;
   case "Wait":if(!step||a.WaitMilliseconds<0||a.WaitMilliseconds>10000)throw new ArgumentException(UiText.Get("Ожидание шага: 0–10000 мс."));break;
   case "Sequence":if(step||a.Steps==null||a.Steps.Count<1||a.Steps.Count>32)throw new ArgumentException(UiText.Get("Последовательность: 1–32 шага, без вложений."));int total=0;foreach(var s in a.Steps){Validate(s,true);total+=s.WaitMilliseconds;if(total>60000)throw new ArgumentException(UiText.Get("Суммарное ожидание: не более 60000 мс."));}break;
   default:throw new ArgumentException(UiText.Get("Неизвестное действие."));
  }
  if(a.Action!="Sequence"&&a.Steps!=null&&a.Steps.Count!=0)throw new ArgumentException(UiText.Get("Вложенные шаги недопустимы."));
  if(a.Action!="Wait"&&a.WaitMilliseconds!=0)throw new ArgumentException(UiText.Get("Ожидание задаётся отдельным шагом."));
 }
}
