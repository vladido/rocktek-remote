using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
/// <summary>Display language is a per-user UI preference, separate from button mappings.</summary>
public static class UiText {
 static string language="en";
 static string preferenceFile=DefaultPreferenceFile;
 public static string DefaultPreferenceFile {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RockTekRemote","ui-language.txt");}}
 public static string Language {get{return language;}set{if(value!="en"&&value!="ru")throw new ArgumentException("Language must be en or ru.");language=value;}}
 static readonly Dictionary<string,string> English=new Dictionary<string,string> {
  {"Укажите Bluetooth-адрес пульта в ROCKTEK_REMOTE_DEVICE (12 шестнадцатеричных символов), затем перезапустите программу.", "Set the remote Bluetooth address in ROCKTEK_REMOTE_DEVICE (12 hexadecimal characters), then restart the application."},
  {"Отмена", "Cancel"},
  {"Нажмите клавишу или Ctrl / Alt / Shift + клавишу на клавиатуре. Esc — отмена.", "Press a key or Ctrl / Alt / Shift + key. Esc cancels."},
  {"Запись сочетания отменена. Прежнее значение сохранено.", "Recording cancelled. The previous shortcut is unchanged."},
  {"Записать сочетание", "Record shortcut"},
  {"Сочетания с Win вводятся вручную; Windows может перехватить их. Esc — отмена.", "Enter Win shortcuts manually; Windows may intercept them. Esc cancels."},
  {"Эта клавиша не поддерживается. Нажмите другую клавишу; Esc — отмена.", "Unsupported key. Press another key; Esc cancels."},
  {"Записано: ", "Recorded: "},
  {". Нажмите Сохранить, чтобы применить назначение.", ". Click Save to apply the assignment."},
  {"код 0x", "code 0x"},
  {"Клавиатура · ", "Keyboard · "},
  {"Мультимедиа · ", "Consumer · "},
  {"RockTek Remote · Настройки", "RockTek Remote · Settings"},
  {"Кнопки пульта RC-G2  /  персональные действия", "RC-G2 remote buttons / custom actions"},
  {"Тёмная тема", "Dark theme"},
  {"Файл настроек", "Settings file"},
  {"Файл настроек · --config (изолированно)", "Settings file · --config (isolated)"},
  {"Изменить…", "Change…"},
  {"По умолчанию", "Default"},
  {"Добавить кнопку", "Add button"},
  {"Удалить кнопку", "Remove button"},
  {"Везде", "Everywhere"},
  {"Только в выбранном приложении", "Only in selected application"},
  {"Домой", "Home"},
  {"Назад", "Back"},
  {"Вверх", "Up"},
  {"Вниз", "Down"},
  {"Влево", "Left"},
  {"Вправо", "Right"},
  {"Повторять при удержании", "Repeat while held"},
  {"Вход пульта", "Remote input"},
  {"Действие", "Action"},
  {"Клавиша или сочетание клавиш", "Key or key combination"},
  {"Область", "Scope"},
  {"Приложение (*.exe)|*.exe", "Application (*.exe)|*.exe"},
  {"В каком приложении кнопке разрешено срабатывать", "Application where this button is allowed to work"},
  {"Удержание", "Hold"},
  {"Задержка, мс", "Delay, ms"},
  {"Интервал, мс", "Interval, ms"},
  {"Некоторые кнопки пульта также выполняют штатное действие Windows.\nНазначение в этой программе не отменяет его.", "Some remote buttons also perform their native Windows action.\nAssigning an action here does not cancel it."},
  {"Сохранить", "Save"},
  {"Сбросить", "Reset"},
  {"Вернуть стандартные кнопки? Изменения будут записаны только после Сохранить.", "Restore default buttons? Changes are written only after Save."},
  {"Стандартные кнопки восстановлены; нажмите Сохранить.", "Default buttons restored; click Save."},
  {"Импорт…", "Import…"},
  {"Экспорт…", "Export…"},
  {"Закрыть", "Close"},
  {"Файл настроек: ", "Settings file: "},
  {"Настройки JSON (*.json)|*.json", "JSON settings (*.json)|*.json"},
  {"Выбрать основной файл настроек", "Select the primary settings file"},
  {"Загрузить настройки из выбранного файла? Он не будет перезаписан. Несохранённые изменения в окне будут заменены.", "Load the selected file? It will not be overwritten. Unsaved editor changes will be replaced."},
  {"Создать основной файл с текущими настройками? Пути приложений будут сохранены абсолютными, чтобы перенос не менял их смысл.", "Create a primary file with current settings? Application paths will be made absolute to preserve their meaning."},
  {"Сначала завершите или отмените обучение.", "Finish or cancel learning first."},
  {"Основной файл выбран: ", "Primary settings file selected: "},
  {". Обработчик подхватит его автоматически.", ". The handler will pick it up automatically."},
  {"Выбор основного файла не завершён: ", "Primary file selection failed: "},
  {"Основной файл изменён в другом окне. Откройте настройки заново или выберите файл через Изменить….", "The primary file was changed in another window. Reopen settings or select it with Change…."},
  {"Файл настроек не выбран: исправьте указатель через Изменить… или По умолчанию.", "No settings file selected: repair the locator with Change… or Default."},
  {"Сохранено в ", "Saved to "},
  {"Не сохранено: ", "Not saved: "},
  {"Кнопка удалена; нажмите Сохранить.", "Button removed; click Save."},
  {"Отмена обучения", "Cancel learning"},
  {"Нажмите одну кнопку на пульте — ожидание 30 секунд. Обработчик v2 временно приостановлен.", "Press one remote button within 30 seconds. The handler is temporarily paused."},
  {" · Кнопка уже есть: ", " · Button already exists: "},
  {". Дубликат не добавлен.", ". No duplicate was added."},
  {"; подтвердите имя. До подтверждения файл не изменён.", "; confirm a name. The file is unchanged until confirmation."},
  {"Добавлено без действия; выберите действие и Сохранить. Нативное действие Windows может остаться.", "Added without an action; choose an action and Save. Native Windows actions may still occur."},
  {"Событий за 30 секунд нет. Если кнопка не передаёт поддерживаемое Bluetooth-событие, добавить её нельзя. Попробуйте ещё раз.", "No event received in 30 seconds. A button needs a supported Bluetooth event to be added. Try again."},
  {"Имя новой кнопки — ", "New button name — "},
  {"Имя кнопки на пульте", "Remote button name"},
  {"Введите имя кнопки на пульте, например: 1. Некоторые кнопки пульта также выполняют штатное действие Windows. Назначение в этой программе не отменяет его.", "Enter the physical button name, for example: 1. Some buttons also perform native Windows actions; assigning an action here does not cancel them."},
  {"Диагностика", "Diagnostics"},
  {"Диагностика сигнала", "Signal diagnostics"},
  {"Добавить", "Add"},
  {"Ошибка обучения: ", "Learning error: "},
  {"Импортировано; нажмите Сохранить для записи в ", "Imported; click Save to write to "},
  {"Экспорт: ", "Export: "},
  {"--config: укажите файл.", "--config: specify a file."},
  {"Файл настроек: указатель недоступен или повреждён. ", "Settings file: the locator is unavailable or damaged. "},
  {" Выберите файл через Изменить…; сохранение заблокировано до выбора.", " Select a file with Change…; saving is blocked until selection."},
  {"Выберите целевое приложение .exe в настройках.", "Select a target application .exe in settings."},
  {"Укажите клавишу, например Ctrl+Right.", "Specify a key, for example Ctrl+Right."},
  {"Неизвестный модификатор: ", "Unknown modifier: "},
  {"Модификатор указан дважды.", "Duplicate modifier."},
  {"Неизвестная клавиша: ", "Unknown key: "},
  {". Используйте латинские имена клавиш.", ". Use English key names."},
  {"Неподдерживаемая версия настроек.", "Unsupported settings version."},
  {"Нужны стандартные кнопки; максимум 256 кнопок.", "Default buttons are required; at most 256 buttons are allowed."},
  {"Стандартная кнопка или usage изменены: ", "Default button or usage changed: "},
  {"Имя кнопки: 1–48 символов, без дублей.", "Button names must be unique and 1–48 characters long."},
  {"Недопустимое имя кнопки.", "Invalid button name."},
  {"InputType Consumer/Keyboard и код: без дублей источника+кода.", "InputType must be Consumer/Keyboard with unique source + code pairs."},
  {"Некорректные метаданные клавиатуры.", "Invalid keyboard metadata."},
  {"Неизвестная область: ", "Unknown scope: "},
  {"Кнопка «", "Button “"},
  {"»: выберите .exe для области «Только в выбранном приложении».", "”: select an .exe for the Only in selected application scope."},
  {"Mute работает нативно; переназначение небезопасно.", "Mute is native; reassignment is unsafe."},
  {"Native доступно только для Mute.", "Native is available only for Mute."},
  {"Повтор: задержка 100–5000 мс, интервал 50–2000 мс.", "Repeat: delay 100–5000 ms, interval 50–2000 ms."},
  {"Повтор: только Клавиша, кроме Home/Back/OK/Mute.", "Repeat is for Key actions only, excluding Home/Back/OK/Mute."},
  {"Пустые настройки.", "Empty settings."},
  {"Неизвестный формат настроек.", "Unknown settings format."},
  {"Повреждён каталог v2.", "Damaged button catalog."},
  {"Выберите .exe для каждой ограниченной кнопки, затем Сохранить.", "Select an .exe for each restricted button, then Save."},
  {"Требуется настройка: ", "Configuration required: "},
  {" Исходный файл сохранён; действия не изменены.", " The original file is preserved; actions are unchanged."},
  {"Настройки не загружены: ", "Settings not loaded: "},
  {" Используются стандартные; исходный файл сохранён.", " Using defaults; the original file is preserved."},
  {"Укажите полный путь к JSON настроек.", "Specify the full path to the settings JSON."},
  {"Выберите файл JSON, не папку.", "Select a JSON file, not a folder."},
  {"Недопустимый путь JSON.", "Invalid JSON path."},
  {"Слишком большой указатель настроек.", "Settings locator is too large."},
  {"Некорректный указатель настроек.", "Invalid settings locator."},
  {"Выбранный файл настроек отсутствует: ", "Selected settings file is missing: "},
  {"Указатель и файл настроек должны быть разными файлами.", "The locator and settings must be different files."},
  {"Обучение уже открыто в другом окне.", "Learning is already active in another window."},
  {"Не удалось проверить путь: ", "Unable to verify path: "},
  {"Перенаправленный путь запрещён: ", "Redirected path is not allowed: "},
  {"RockTek Remote — настройки пульта", "RockTek Remote — remote settings"},
  {"Не удалось завершить обработчик.", "Unable to stop the handler."},
  {"Удалить RockTek Remote? Настройки, OneDrive и выбранные приложения сохранятся.", "Remove RockTek Remote? Settings, OneDrive and selected applications will be preserved."},
  {"Укажите обычный локальный путь без сетевых и служебных адресов.", "Specify a normal local path, not a network or device address."},
  {"Путь диска должен быть абсолютным.", "Drive paths must be absolute."},
  {"Нужен локальный путь диска.", "A local drive path is required."},
  {"Служебные имена устройств запрещены.", "Reserved device names are not allowed."},
  {"Для ссылки разрешены только http и https.", "Only http and https links are allowed."},
  {"Некорректная ссылка.", "Invalid link."},
  {"Программы запускайте действием Открыть приложение; исполняемые файлы и сценарии здесь запрещены.", "Use Open application for programs; executables and scripts are not allowed here."},
  {"Пустое действие.", "Empty action."},
  {"Недопустимый шаг последовательности.", "Invalid sequence step."},
  {"Выберите приложение .exe.", "Select an application .exe."},
  {"Некорректные аргументы.", "Invalid arguments."},
  {"Неизвестное действие мыши.", "Unknown mouse action."},
  {"Прокрутка: от −100 до 100, кроме нуля.", "Scroll: −100 to 100, excluding zero."},
  {"Ожидание шага: 0–10000 мс.", "Step wait: 0–10000 ms."},
  {"Последовательность: 1–32 шага, без вложений.", "Sequence: 1–32 steps, no nested sequences."},
  {"Суммарное ожидание: не более 60000 мс.", "Total wait: at most 60000 ms."},
  {"Неизвестное действие.", "Unknown action."},
  {"Вложенные шаги недопустимы.", "Nested steps are not allowed."},
  {"Ожидание задаётся отдельным шагом.", "Wait must be a separate step."},
  {"RockTek Remote · Установка", "RockTek Remote · Setup"},
  {"RockTek Remote 2.0\n\nУстановка только для текущего пользователя, без администратора.\n\nПрограмма появится в меню Пуск и списке приложений Windows. Настройки, выбранные приложения и резервные копии OneDrive сохраняются.", "RockTek Remote 2.0\n\nInstall for the current user without administrator access.\n\nThe application will appear in the Start menu and Windows Apps. Settings, selected applications and OneDrive backups are preserved."},
  {"Установить", "Install"},
  {"Установлено. Настройки доступны в меню Пуск. Автозапуск — при следующем входе в Windows.", "Installed. Open settings from the Start menu. Autostart begins at the next sign-in."},
  {"Ошибка установки", "Installation error"},
  {"Ресурс значка не найден.", "Icon resource not found."},
  {"Левый щелчок", "Left click"},
  {"Правый щелчок", "Right click"},
  {"Двойной щелчок", "Double click"},
  {"Прокрутка", "Scroll"},
  {"Редактировать шаги…", "Edit steps…"},
  {"Редактировать шаги… (", "Edit steps… ("},
  {"Выберите папку", "Select a folder"},
  {"Файл (*.*)|*.*", "File (*.*)|*.*"},
  {"Выберите приложение .exe", "Select an application .exe"},
  {"Выберите файл", "Select a file"},
  {"Какое приложение кнопка должна открыть", "Application this button should open"},
  {"Дополнительные параметры запуска", "Additional launch options"},
  {"Аргументы", "Arguments"},
  {"Рабочая папка", "Working directory"},
  {"Показать окно или запустить EXE.", "Show an existing window or launch the EXE."},
  {"Файл / папка / URL", "File / folder / URL"},
  {"Выбрать папку…", "Choose folder…"},
  {"Файл или папка открываются штатной программой; ссылки — только http / https.", "Files and folders use their default application; links must use http / https."},
  {"Действие мыши", "Mouse action"},
  {"Щелчок только в текущем положении курсора. + вверх, − вниз (деления колеса).", "Click at the current pointer only. + scrolls up, − down (wheel notches)."},
  {"Последовательность", "Sequence"},
  {"1–32 шага; ожидание до 10 секунд за шаг, суммарно до 60 секунд.", "1–32 steps; waits up to 10 seconds per step and 60 seconds total."},
  {"Ожидание, мс", "Wait, ms"},
  {"Открыть приложение", "Open application"},
  {"Открыть файл / папку / ссылку", "Open file / folder / link"},
  {"Мышь (текущий курсор)", "Mouse (current pointer)"},
  {"Ожидание", "Wait"},
  {"Клавиша / сочетание", "Key / shortcut"},
  {"Без действия", "Disabled"},
  {"Штатное действие", "Native action"},
  {"Последовательность действий", "Action sequence"},
  {"Готово", "Done"},
  {"Удалить", "Remove"},
  {"Максимум 32 шага.", "At most 32 steps."},
  {"Открыть настройки", "Open settings"},
  {"Выход", "Exit"},
  {"Обзор…", "Browse…"},
  {"Целевое приложение больше не активно; оставшиеся шаги отменены.", "The target application is no longer active; remaining steps cancelled."},
  {", ошибка ", ", error "},
  {"Windows отказала в переключении окна приложения.", "Windows refused to activate the application window."},
  {"Приложение не найдено.", "Application not found."},
  {"Рабочая папка не найдена.", "Working directory not found."},
  {"Файл или папка не найдены.", "File or folder not found."},
 };
 public static string Get(string russian){string english;return language=="en"&&English.TryGetValue(russian,out english)?english:russian;}
 public static void Initialize(string[] args){Initialize(args,DefaultPreferenceFile);}
 public static void Initialize(string[] args,string preferences){
  preferenceFile=Path.GetFullPath(preferences);
  Language=CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="ru"?"ru":"en";
  try{FileSafety.CheckPath(preferenceFile);if(File.Exists(preferenceFile)&&new FileInfo(preferenceFile).Length<=16){string saved=File.ReadAllText(preferenceFile).Trim();if(saved=="ru"||saved=="en")Language=saved;}}catch(IOException){}catch(UnauthorizedAccessException){}
  for(int i=0;i<args.Length;i++)if(args[i]=="--language"){if(i+1>=args.Length)throw new ArgumentException("--language: specify en or ru.");Language=args[++i];}
 }
 public static void SelectLanguage(string value){
  if(value!="en"&&value!="ru")throw new ArgumentException("Language must be en or ru.");
  FileSafety.CheckPath(preferenceFile);Directory.CreateDirectory(Path.GetDirectoryName(preferenceFile));
  string temporary=preferenceFile+"."+Guid.NewGuid().ToString("N")+".new";
  try{using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new StreamWriter(output))writer.Write(value);FileSafety.CheckPath(preferenceFile);if(File.Exists(preferenceFile))File.Replace(temporary,preferenceFile,null);else File.Move(temporary,preferenceFile);Language=value;}finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 public static string TranslateDisplay(string text,string previousLanguage){
  if(previousLanguage==Language)return text;
  if(text.StartsWith("▾ ")||text.StartsWith("▸ "))return text.Substring(0,2)+TranslateDisplay(text.Substring(2),previousLanguage);
  foreach(var entry in English){string from=previousLanguage=="ru"?entry.Key:entry.Value;if(text==from)return Language=="ru"?entry.Key:entry.Value;}
  string russianPrefix="Редактировать шаги… (";string prefix=previousLanguage=="ru"?russianPrefix:English[russianPrefix];
  if(text.StartsWith(prefix,StringComparison.Ordinal))return (Language=="ru"?russianPrefix:English[russianPrefix])+text.Substring(prefix.Length);
  // Unrecognized text can contain a path or a user-provided name: leave it intact.
  return text;
 }
 public static void Apply(Control root,string previousLanguage){
  if(previousLanguage==Language)return;
  // Never alter editable values, mapping names, paths or serialized action tokens.
  if(!(root is TextBoxBase)&&!(root is ListControl)&&!(root is NumericUpDown)){
   string original=root.Text;
   root.Text=TranslateDisplay(original,previousLanguage);
   if(!String.IsNullOrEmpty(root.AccessibleName)){string translated; if(Language=="en"&&English.TryGetValue(root.AccessibleName,out translated))root.AccessibleName=translated;else if(Language=="ru")foreach(var entry in English)if(entry.Value==root.AccessibleName){root.AccessibleName=entry.Key;break;}}
  }
  foreach(Control child in root.Controls)Apply(child,previousLanguage);
  root.Refresh();
 }
}
