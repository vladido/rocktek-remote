using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Forms;
public sealed class ButtonConfig : ActionSpec {
 public string InputType {get;set;} public int ScanCode {get;set;} public int ScanFlags {get;set;} public int Usage {get;set;} public string Scope {get;set;} public string ScopeApplicationPath {get;set;} public bool Repeat {get;set;} public int Delay {get;set;} public int Interval {get;set;}
 public ButtonConfig(){InputType="Consumer";Action="Disabled";Key="Space";Scope="Application";ScopeApplicationPath="";Delay=350;Interval=150;}
}
public sealed class KeyCombination {
 public ushort Key; public ushort[] Modifiers;
 public static KeyCombination Parse(string value){
  if(String.IsNullOrWhiteSpace(value))throw new ArgumentException(UiText.Get("Укажите клавишу, например Ctrl+Right."));
  var parts=value.Split('+');var mods=new List<ushort>();
  for(int i=0;i<parts.Length-1;i++){ushort m;switch(parts[i].Trim().ToUpperInvariant()){case "CTRL":m=17;break;case "SHIFT":m=16;break;case "ALT":m=18;break;case "WIN":m=91;break;default:throw new ArgumentException(UiText.Get("Неизвестный модификатор: ")+parts[i]);}if(mods.Contains(m))throw new ArgumentException(UiText.Get("Модификатор указан дважды."));mods.Add(m);}
  string key=parts[parts.Length-1].Trim();var names=new Dictionary<string,ushort>(StringComparer.OrdinalIgnoreCase){{"Space",32},{"Enter",13},{"Escape",27},{"Esc",27},{"Tab",9},{"Backspace",8},{"Up",38},{"Down",40},{"Left",37},{"Right",39},{"Home",36},{"End",35},{"PageUp",33},{"PageDown",34},{"Insert",45},{"Delete",46},{"VolumeUp",175},{"VolumeDown",174},{"VolumeMute",173},{"MediaPlayPause",179},{"MediaNextTrack",176},{"MediaPreviousTrack",177},{"BrowserHome",172}};
  ushort vk;int f;if(names.TryGetValue(key,out vk)){}else if(key.Length==1&&Char.IsLetterOrDigit(key[0])&&key[0]<128){char k=Char.ToUpperInvariant(key[0]);vk=(ushort)k;}else if(key.StartsWith("F",StringComparison.OrdinalIgnoreCase)&&Int32.TryParse(key.Substring(1),out f)&&f>=1&&f<=24)vk=(ushort)(111+f);else throw new ArgumentException(UiText.Get("Неизвестная клавиша: ")+key+UiText.Get(". Используйте латинские имена клавиш."));
  return new KeyCombination{Key=vk,Modifiers=mods.ToArray()};
 }
}
public sealed class RemoteConfig {
 public int Version {get;set;} public string TargetApplicationPath {get;set;} [ScriptIgnore] public string MurglarPath {get{return TargetApplicationPath;}set{TargetApplicationPath=value;}} public Dictionary<string,ButtonConfig> Buttons {get;set;}
 public static readonly string[] Names={"Home","Back","OK","Up","Down","Left","Right","CH+","CH-","Mute"};
 public static readonly ushort[] Usages={0x223,0x224,0x41,0x42,0x43,0x44,0x45,0x9C,0x9D,0xE2};
 public static RemoteConfig Defaults(){var c=new RemoteConfig{Version=4,TargetApplicationPath="",Buttons=new Dictionary<string,ButtonConfig>()};string[] keys={"Space","Escape","Space","Up","Down","Left","Right","VolumeUp","VolumeDown","Space"};for(int i=0;i<Names.Length;i++)c.Buttons[Names[i]]=new ButtonConfig{Usage=Usages[i],Action=i==0?"OpenTargetApplication":i==9?"Native":"Key",Key=keys[i],Scope=i==0||i>=7?"Global":"Application",Repeat=i>=3&&i<=8,Delay=350,Interval=150};return c;}
 public void Validate(){Validate(false);}
 void Validate(bool allowMissingScopes){
 if(Version!=4)throw new ArgumentException(UiText.Get("Неподдерживаемая версия настроек."));
 if(Buttons==null||Buttons.Count<Names.Length||Buttons.Count>256)throw new ArgumentException(UiText.Get("Нужны стандартные кнопки; максимум 256 кнопок."));
 foreach(string name in Names){ButtonConfig b;int i=Array.IndexOf(Names,name);if(!Buttons.TryGetValue(name,out b)||b==null||b.InputType!="Consumer"||b.Usage!=Usages[i])throw new ArgumentException(UiText.Get("Стандартная кнопка или usage изменены: ")+name);}
 var usages=new HashSet<string>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 foreach(var entry in Buttons){string name=entry.Key;ButtonConfig b=entry.Value;
 if(String.IsNullOrWhiteSpace(name)||name!=name.Trim()||name.Length>48||!names.Add(name))throw new ArgumentException(UiText.Get("Имя кнопки: 1–48 символов, без дублей."));foreach(char ch in name)if(Char.IsControl(ch))throw new ArgumentException(UiText.Get("Недопустимое имя кнопки."));
 if(b==null||(b.InputType!="Consumer"&&b.InputType!="Keyboard")||b.Usage<1||b.Usage>(b.InputType=="Keyboard"?254:65535)||!usages.Add(b.InputType+":"+b.Usage))throw new ArgumentException(UiText.Get("InputType Consumer/Keyboard и код: без дублей источника+кода."));
 if(b.ScanCode<0||b.ScanCode>65535||b.ScanFlags<0||b.ScanFlags>6||(b.ScanFlags&1)!=0)throw new ArgumentException(UiText.Get("Некорректные метаданные клавиатуры."));
 ActionValidation.Validate(b,false);
 if(b.Scope!="Global"&&b.Scope!="Application")throw new ArgumentException(UiText.Get("Неизвестная область: ")+name);
 if(b.Scope=="Application"){if(String.IsNullOrWhiteSpace(b.ScopeApplicationPath)&&!allowMissingScopes)throw new ArgumentException(UiText.Get("Кнопка «")+name+UiText.Get("»: выберите .exe для области «Только в выбранном приложении»."));if(!String.IsNullOrWhiteSpace(b.ScopeApplicationPath))ActionValidation.Validate(new ActionSpec{Action="LaunchApplication",ApplicationPath=b.ScopeApplicationPath},false);}else if(!String.IsNullOrWhiteSpace(b.ScopeApplicationPath))ActionValidation.Validate(new ActionSpec{Action="LaunchApplication",ApplicationPath=b.ScopeApplicationPath},false);
 if(name=="Mute"&&b.Action!="Native")throw new ArgumentException(UiText.Get("Mute работает нативно; переназначение небезопасно."));if(name!="Mute"&&b.Action=="Native")throw new ArgumentException(UiText.Get("Native доступно только для Mute."));
 if(b.Delay<100||b.Delay>5000||b.Interval<50||b.Interval>2000)throw new ArgumentException(UiText.Get("Повтор: задержка 100–5000 мс, интервал 50–2000 мс."));
 if(b.Repeat&&(name=="Home"||name=="Back"||name=="OK"||name=="Mute"||b.Action!="Key"))throw new ArgumentException(UiText.Get("Повтор: только Клавиша, кроме Home/Back/OK/Mute."));if(b.Action=="Key")KeyCombination.Parse(b.Key);
 }if(TargetApplicationPath==null)TargetApplicationPath="";if(!String.IsNullOrWhiteSpace(TargetApplicationPath))ActionValidation.Validate(new ActionSpec{Action="LaunchApplication",ApplicationPath=TargetApplicationPath},false);
 }
 public static RemoteConfig Parse(string json){return Parse(json,false);}
 static RemoteConfig Parse(string json,bool allowMissingScopes){var ser=new JavaScriptSerializer();var raw=ser.Deserialize<Dictionary<string,object>>(json);if(raw==null)throw new ArgumentException(UiText.Get("Пустые настройки."));RemoteConfig c;if(!raw.ContainsKey("Version")&&!raw.ContainsKey("Buttons")){if(raw.Count>1||!raw.ContainsKey("MurglarPath"))throw new ArgumentException(UiText.Get("Неизвестный формат настроек."));c=Defaults();c.MurglarPath=Convert.ToString(raw["MurglarPath"]);}else {int version=raw.ContainsKey("Version")?Convert.ToInt32(raw["Version"]):0;if(version==2||version==3){if(raw.ContainsKey("MurglarPath")&&!raw.ContainsKey("TargetApplicationPath"))raw["TargetApplicationPath"]=raw["MurglarPath"];json=ser.Serialize(raw);}c=ser.Deserialize<RemoteConfig>(json);if(c.Version==2){if(c.Buttons==null||c.Buttons.Count!=Names.Length)throw new ArgumentException(UiText.Get("Повреждён каталог v2."));for(int i=0;i<Names.Length;i++){ButtonConfig b;if(!c.Buttons.TryGetValue(Names[i],out b)||b==null)throw new ArgumentException(UiText.Get("Повреждён каталог v2."));b.Usage=Usages[i];}c.Version=3;}if(c.Version==3){foreach(var b in c.Buttons.Values){if(b.Scope=="Murglar")b.Scope="Application";if(b.Action=="OpenMurglar")b.Action="OpenTargetApplication";}c.Version=4;}}foreach(var entry in c.Buttons){var b=entry.Value;if(b.Scope=="Murglar"||b.Scope=="Player"||b.Scope=="player"||b.Scope=="application")b.Scope="Application";var catalog=raw.ContainsKey("Buttons")?raw["Buttons"] as Dictionary<string,object>:null;var original=catalog!=null&&catalog.ContainsKey(entry.Key)?catalog[entry.Key] as Dictionary<string,object>:null;if(b.Scope=="Application"&&(original==null||!original.ContainsKey("ScopeApplicationPath")))b.ScopeApplicationPath=c.TargetApplicationPath??"";if(b.Action=="OpenTargetApplication"&&String.IsNullOrWhiteSpace(b.ApplicationPath))b.ApplicationPath=c.TargetApplicationPath??"";}c.Validate(allowMissingScopes);return c;}
 public string Json(){Validate();var ser=new JavaScriptSerializer();var raw=ser.Deserialize<Dictionary<string,object>>(ser.Serialize(this));raw.Remove("TargetApplicationPath");return ser.Serialize(raw);}
 public void Relocate(string oldSettings){if(!String.IsNullOrWhiteSpace(TargetApplicationPath))TargetApplicationPath=ActionValidation.ResolvePath(TargetApplicationPath,oldSettings);foreach(var b in Buttons.Values){if(!String.IsNullOrWhiteSpace(b.ScopeApplicationPath))b.ScopeApplicationPath=ActionValidation.ResolvePath(b.ScopeApplicationPath,oldSettings);ActionValidation.Relocate(b,oldSettings);}}
 public static RemoteConfig Load(string path){return Parse(File.ReadAllText(path));}
 public static RemoteConfig LoadSafe(string path,out string error){error="";try{if(!File.Exists(path)){error=UiText.Get("Выберите .exe для каждой ограниченной кнопки, затем Сохранить.");return Defaults();}var c=Parse(File.ReadAllText(path),true);try{c.Validate();}catch(ArgumentException ex){error=UiText.Get("Требуется настройка: ")+ex.Message+UiText.Get(" Исходный файл сохранён; действия не изменены.");}return c;}catch(Exception ex){error=UiText.Get("Настройки не загружены: ")+ex.Message+UiText.Get(" Используются стандартные; исходный файл сохранён.");return Defaults();}}
 public void Save(string path){string json=Json();string full=Path.GetFullPath(path);FileSafety.CheckPath(full);Directory.CreateDirectory(Path.GetDirectoryName(full));FileSafety.CheckPath(full);string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var fs=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] bytes=new System.Text.UTF8Encoding(false).GetBytes(json);fs.Write(bytes,0,bytes.Length);fs.Flush(true);}if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public ButtonConfig Find(ushort usage){return FindInput("Consumer",usage);}
 public ButtonConfig FindInput(string source,int code){foreach(var b in Buttons.Values)if(b.InputType==source&&b.Usage==code)return b;return null;}
}
