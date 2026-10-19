namespace SoulTracker.App;
internal static class HelpText
{
    private static readonly Dictionary<string,(string Ru,string En)> Texts=new()
    {
        ["autoTrack"]=("После получения души добавляет незавершённого моба в HUD. Работает на известных открытых территориях и в Бездне; в подземельях и при неизвестной локации не добавляет. Старые сохранения и вход в игру не считаются получением души.","Adds an incomplete mob to HUD after receiving a soul. Works in known open-world zones and Abyss, excluding dungeons and unknown zones. Loading saved progress or logging in does not count as a soul gain."),
        ["collection"]=("Коллекция питомцев, категории, выбор целей и управление HUD.","Pet collection, categories, target selection and HUD controls."),
        ["settings"]=("Язык, горячие клавиши и размер строк счётчика. Настройки сохраняются автоматически, клавиши — кнопкой «Применить».","Language, hotkeys and counter row size. Settings save automatically; hotkeys use Apply."),
        ["exit"]=("Полностью закрывает трекер, счётчик и карту. Прогресс сохраняется. Крестик главного окна только убирает его в трей.","Closes the tracker, counter and map, saving progress. The main window's X only hides it in the tray."),
        ["saved"]=("Открывает папку с сохранённым прогрессом и настройками на этом компьютере.","Opens the folder containing saved progress and settings on this PC."),
        ["hud"]=("Показывает или скрывает счётчик душ. Отметь цели в колонке HUD или включи режим фокуса.","Shows or hides the soul counter. Pin HUD targets or enable focus mode."),
        ["pin"]=("Отметь мобов для счётчика и карты. Сними все отметки, чтобы карта показывала незавершённых по категориям. Режим фокуса временно выбирает цели автоматически в обоих окнах.","Pin mobs for the counter and map. Clear all pins to browse incomplete targets by category. Focus temporarily selects targets automatically in both windows."),
        ["map"]=("Показывает интерактивную карту мест появления. Без выбранных целей показывает незавершённых мобов отмеченных категорий.","Shows the interactive spawn map. Without pinned targets it shows incomplete mobs in enabled categories."),
        ["lock"]=("Пропускает клики через счётчик и карту в игру. Чтобы снова двигать и настраивать окна, выключи режим здесь или горячей клавишей.","Passes clicks through the counter and map to the game. Turn it off here or with its hotkey to edit and move the windows."),
        ["opacity"]=("Меняет плотность фона счётчика и карты; текст остаётся читаемым.","Changes the counter and map background opacity while keeping text readable."),
        ["focus"]=("В текущей локации игрока автоматически выбирает незавершённых мобов отмеченных категорий, которым осталось меньше всего душ до MAX. При равенстве показывает всех лидеров. Ручные отметки HUD сохраняются.","In the player’s current location, selects incomplete mobs in enabled categories with the fewest souls remaining to MAX. Shows every tie. Your manual HUD pins are preserved."),
        ["category"]=("Включает категорию в списке и на карте, а также в режиме фокуса. Без выбранных целей HUD карта показывает всех незавершённых этой категории.","Enables this category in the list, map and focus mode. Without HUD pins, the map shows every incomplete target in this category."),
        ["special"]=("Показывает диковинных питомцев в каталоге. У них нет обычной цепочки сбора душ, поэтому они не входят в фарм на карте и режим фокуса.","Shows special pets in the catalogue. They do not follow regular soul progression and are excluded from farming maps and focus mode."),
        ["search"]=("Ищет по русскому или английскому имени либо ID. Отмеченные цели всегда наверху, даже вне фильтров. Текстовый поиск меняет только список; карта и фокус продолжают учитывать категории.","Searches Russian/English names or ID. Pinned targets stay at the top even outside filters. Text search filters only the list; map and focus still use categories."),
        ["max"]=("Скрывает завершённых питомцев в списке. На карте и в режиме фокуса MAX всегда исключены.","Hides completed pets in the list. Map and focus always exclude MAX pets."),
        ["owned"]=("Оставляет в списке уже полученных питомцев. Не меняет карту или режим фокуса.","Shows only owned pets in the list. Does not change the map or focus mode."),
        ["language"]=("Меняет язык интерфейса сразу. Прогресс и выбранные цели сохраняются.","Changes the interface language immediately without changing progress or selected targets."),
        ["hotkey"]=("Нажми на поле и зажми нужное сочетание. Используй Ctrl, Alt, Shift или Win с клавишей, либо F1–F24 отдельно. Затем нажми «Применить». Esc отменяет ввод.","Click the field and press a shortcut. Use Ctrl, Alt, Shift or Win with a key, or F1–F24 alone. Then click Apply. Esc cancels editing."),
        ["apply"]=("Проверяет и сохраняет все три сочетания. Если клавиша занята, прежние назначения остаются активными.","Checks and saves all three shortcuts. If one is unavailable, the previous bindings stay active."),
        ["resetKeys"]=("Возвращает в поля Ctrl+Alt+H, Ctrl+Alt+M и Ctrl+Alt+L. Нажми «Применить», чтобы сохранить.","Restores Ctrl+Alt+H, Ctrl+Alt+M and Ctrl+Alt+L in the fields. Click Apply to save."),
        ["scale"]=("Меняет размер имён, чисел и полосок внутри счётчика. Размер самого окна не меняется.","Changes names, numbers and bars inside the counter without resizing its window."),
        ["catalog"]=("Открывает главное окно трекера.","Opens the tracker's main window."),
        ["hideHud"]=("Скрывает только счётчик. Вернуть его можно из главного окна, трея или горячей клавишей.","Hides only the counter. Restore it from the main window, tray or hotkey."),
        ["hideMap"]=("Скрывает только карту. Вернуть её можно из главного окна, трея или горячей клавишей.","Hides only the map. Restore it from the main window, tray or hotkey."),
        ["mapOptions"]=("Раскрывает выбор территории, целей и дополнительные сведения о карте.","Expands territory and target selection and map details."),
        ["world"]=("Выбирает территорию вручную и отключает следование за игроком. Кнопка «Я» включает следование снова.","Selects a territory manually and stops following the player. Me enables following again."),
        ["target"]=("Сужает карту до одной из отмеченных целей HUD. Первый пункт возвращает все отмеченные цели. В режиме фокуса цели выбираются автоматически.","Narrows the map to one pinned HUD target. The first option restores all pins. Focus mode selects targets automatically."),
        ["all"]=("Индикатор автоматического выбора: без целей HUD и без фокуса показаны все незавершённые по категориям. При выборе целей или фокуса карта сужается автоматически.","Automatic selection indicator: without HUD targets or focus, shows every incomplete target by category. Pins or focus narrow the map automatically."),
        ["follow"]=("Центрирует карту на последней подтверждённой позиции игрока. Координаты могут отставать; их возраст указан внизу.","Centers on the last confirmed player position. Coordinates may lag; their age is shown below."),
        ["fit"]=("Показывает всю выбранную территорию.","Fits the selected territory into the map."),
        ["zoomIn"]=("Приближает карту. Также можно использовать колесо мыши.","Zooms in. You can also use the mouse wheel."),
        ["zoomOut"]=("Отдаляет карту. Также можно использовать колесо мыши.","Zooms out. You can also use the mouse wheel."),
        ["npcap"]=("Открывает официальный сайт Npcap — драйвера для чтения данных игры.","Opens the official Npcap website, the driver used to read game data."),
        ["recheck"]=("Повторно проверяет наличие Npcap и запускает чтение игры, если драйвер установлен.","Checks Npcap again and starts capture if it is installed.")
    };
    internal static string Get(string topic,string language)=>Texts.TryGetValue(topic,out var pair) ? language=="en" ? pair.En : pair.Ru : topic;
}
