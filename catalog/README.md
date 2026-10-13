# Коды дополнительных кнопок RockTek RC-G2

[rc-g2-learned-buttons.json](rc-g2-learned-buttons.json) содержит 25 дополнительных кнопок, проверенных физическими нажатиями и отпусканиями. Это фрагмент каталога, а не полный файл настроек: в нём нет стандартных Home, Back, OK, стрелок, CH+/CH− и Mute.

Все записи имеют `Action: Disabled` и выключенный повтор. Они не добавляют пользовательских действий; штатное поведение Windows этим не гарантированно подавляется.

Для ручного добавления скопируйте нужные записи из `Buttons` в `Buttons` своего основного JSON, сохранив существующие назначения. Не заменяйте основной файл этим фрагментом и не импортируйте его как полную конфигурацию. Если источник и код уже существуют, используйте существующую запись вместо дубля. После изменения откройте настройки и проверьте список. Обработчик перечитывает сохранённый JSON без переустановки.

- Цифры 1–9: Keyboard F2–F10, коды `0x0071`–`0x0079`.
- Цифра 0: Keyboard F11, `0x007A`.
- Субтитры: Consumer `0x0061`.
- Информация: Consumer `0x01BD`.
- Красная / зелёная / жёлтая / синяя: Consumer `0x0069` / `0x006A` / `0x006C` / `0x006B`.
- YouTube / Netflix / Prime Video / Disney+: Consumer `0x0077` / `0x0078` / `0x0079` / `0x007A`.
- Настройки: Consumer `0x0096`.
- Избранное: Consumer `0x022A`.
- Телегид: Consumer `0x008D`.
- Вход / источник: Consumer `0x01BB`.
- Питание: Consumer `0x0030`.

Идентичность входа — пара `InputType` + `Usage`: одинаковые числовые коды Keyboard и Consumer не являются одной кнопкой. В JSON сохранены scan-коды клавиатурных кнопок.

Google Ассистент и отдельные Громкость+/Громкость− не включены: в проверенном клавиатурном и мультимедийном Raw Input потоке от них не обнаружены события. Это не устанавливает отсутствие сигнала на всех возможных путях Windows. Коды другой прошивки могут отличаться.

## English

The JSON contains 25 physically observed additional RC-G2 buttons, including make/release observations. Every entry is disabled and has repeat turned off. It is a partial button catalog, not a complete settings file. Copy selected entries into the `Buttons` object of your existing primary JSON without replacing existing assignments; do not import this fragment as a complete configuration. Existing source/code identities must not be duplicated. The runtime reloads saved JSON without reinstalling the application.

Input identity is the pair `InputType` + `Usage`; equal Keyboard and Consumer numbers represent different inputs. Keyboard scan metadata is retained. Google Assistant and dedicated Volume+/Volume− are omitted because no events were observed at the tested Raw Input boundaries; this does not establish absence at every Windows input boundary. Firmware revisions may use different codes. Disabled mappings do not guarantee suppression of native Windows behavior.
