<p align="center"><img src="https://raw.githubusercontent.com/WasyaPetechkin/Aion2SoulTracker/f94568c87c70902fb26f855d502caa07e8786c86/assets/app.png" width="88" alt="Aion 2 Soul Tracker"></p>
<h1 align="center">Aion 2 Soul Tracker</h1>
<p align="center">Коллекция душ · Компактный HUD · Интерактивная карта<br>Soul collection · Compact HUD · Interactive map</p>
<p align="center"><strong>Windows 10 / 11 x64 · Aion 2 Global EU · Русский / English</strong></p>
<p align="center"><a href="#русский">Русский</a> · <a href="#english">English</a> · <a href="https://github.com/WasyaPetechkin/Aion2SoulTracker/releases/tag/v0.3.5">Скачать / Download 0.3.5</a> · <a href="https://github.com/WasyaPetechkin/Aion2SoulTracker/issues">Обратная связь / Feedback</a></p>

## Русский

**Aion 2 Soul Tracker** помогает собирать души и выбирать следующую цель для фарма. Каталог показывает прогресс коллекции, компактный HUD — сколько осталось до MAX, а отдельная интерактивная карта — известные места появления монстров.

### Скачать и установить

**[Скачать установщик 0.3.5 для Windows](https://github.com/WasyaPetechkin/Aion2SoulTracker/releases/download/v0.3.5/Aion2SoulTracker-Setup-v0.3.5.exe)** · [Все версии и файлы](https://github.com/WasyaPetechkin/Aion2SoulTracker/releases)

1. Запустите `Aion2SoulTracker-Setup-v0.3.5.exe`, установите программу и откройте её с ярлыка.
2. Если приложение попросит **Npcap**, установите его с [официального сайта](https://npcap.com/#download) с настройками по умолчанию, затем нажмите «Проверить снова». Если Npcap уже установлен, повторная установка обычно не нужна.
3. Войдите в персонажа **после запуска трекера**. Если уже играете, вернитесь к выбору персонажа и войдите снова: так приложение получает начальную коллекцию.
4. Включите нужные категории и отметьте цели в колонке **HUD**. Для отображения HUD используйте оконный режим игры или окно без рамки.

**.NET уже включён** — Python, SDK и инструменты разработчика устанавливать не нужно. Npcap устанавливается отдельно. Тестовая сборка пока без цифровой подписи; Windows может показать предупреждение о неизвестном издателе.

| Файл в Releases → Assets | Для чего |
| --- | --- |
| `Aion2SoulTracker-Setup-v0.3.5.exe` | Обычная установка — рекомендуемый вариант |
| `Aion2SoulTracker-v0.3.5-portable-win-x64.zip` | Распаковать целиком в отдельную папку и запустить EXE; Npcap всё равно нужен |
| `Aion2SoulTracker-v0.3.5-source.zip` | Исходный код для разработчиков |
| `SHA256SUMS-v0.3.5.txt` | Контрольные суммы файлов |

### LagoFast и другие ускорители

В 0.3.5 исправлено чтение игровых соединений через локальный посредник LagoFast. Трекер автоматически определяет его порт и использует Loopback. Проверены вход в персонажа и обновление счётчика душ через LagoFast. Запускайте трекер до входа в персонажа; при обновлении полностью закройте старую версию.

Другие ускорители с похожим устройством могут работать, однако отдельно не проверены.

### Возможности

| Функция | Как работает |
| --- | --- |
| Каталог | Русские и английские имена, поиск по имени/ID, категории, фильтры полученных и MAX |
| Отмеченные цели | Всегда сверху каталога, даже если не соответствуют текущему поиску или фильтрам |
| HUD | Портрет, название, территория, прогресс и остаток душ; крестик в строке снимает ручное отслеживание; в фокусе он скрыт |
| Карта | Шесть территорий, масштабирование, перемещение, точки спавна и последняя подтверждённая позиция игрока |
| Подсказки карты | Наведение на точку показывает имя и портрет; при плотном расположении точки объединяются |
| Кибелиски | 162 точки на картах Альтгарда, Морхейма, Вертерона и Элтнена; в Бездне не отображаются |
| Фокус | Выбирает ближайших к MAX в текущей локации игрока одновременно для HUD и карты |
| Автодобавление | При включённой галочке новое получение души добавляет незавершённую цель в HUD; данжи исключены |
| Настройки | RU/EN, свои горячие клавиши, прозрачность фона, независимый масштаб строк HUD 65–150% |
| Трей и справка | Крестик главного окна сворачивает его в трей; кружки «?» объясняют настройки |

### Выбор целей и режим фокуса

- **Нет ручных целей и фокус выключен:** карта автоматически показывает всех незавершённых из включённых категорий.
- **Есть ручные цели:** карта сужается до них; категории продолжают действовать. Снять цель можно в каталоге или крестиком в её строке HUD.
- **Фокус включён:** сравниваются оставшиеся души до MAX у подходящих монстров текущей локации. Если двоим осталось по 5 душ, видны оба. Когда одному останется 4 — остаётся он; после MAX выбирается следующий.
- Ручные отметки сохраняются на время фокуса. При неизвестной локации фокус ждёт её определения, а не выбирает цели другой территории.
- **«Добавлять в HUD при получении души» нужно включить отдельно.** Функция реагирует на новые души в известных открытых локациях. Вход в персонажа, загрузка сохранений и получение душ в данжах цели не добавляют.
- Поиск, «Скрыть MAX» и «Только полученные» фильтруют каталог; отмеченные цели остаются видимыми. Категории также влияют на карту и фокус.

### Территории в каталоге

Колонка **«Территория»** показывает, где известны места появления: Асмодиане, Элийцы или Бездна. При наведении видны названия локаций. Данные доступны и для завершённых мобов: снимите **«Скрыть MAX»**, затем найдите моба по имени или ID. Добавлять его в HUD не нужно.

Фильтр **«Территория»** над поиском ограничивает только каталог. Отмеченные вручную цели остаются видимыми. На HUD, карту и режим фокуса этот фильтр не влияет. «Территория неизвестна» означает отсутствие данных.

### Настройки прямо в HUD

Внизу счётчика нажмите **«Настройки HUD ▾»**. Здесь доступны режим фокуса, автодобавление при получении души, категории, показ карты и «Клики в игру». В подменю «Фильтры каталога» — диковинные, «Скрыть MAX» и «Только полученные». Изменения сразу синхронизируются с главным окном. Категории влияют на карту и фокус; фильтры каталога — только на список в основном окне.

Подсказки меню переносятся на строки и закрываются при уходе мыши, выборе пункта или закрытии меню.

Если меню не нажимается, отключите «Клики в игру» сочетанием `Ctrl+Alt+L` (или своим назначенным сочетанием). В режиме фокуса крестики строк скрыты: цели выбираются автоматически. Выключите фокус, чтобы вернуться к ручным целям.

### Управление

| По умолчанию | Действие |
| --- | --- |
| `Ctrl+Alt+H` | Показать / скрыть HUD |
| `Ctrl+Alt+M` | Показать / скрыть карту |
| `Ctrl+Alt+L` | Пропускать клики в игру / разрешить управление окнами |
| Колесо мыши над картой | Изменить масштаб |
| Перетаскивание карты | Переместить обзор |
| Кнопка «Я» | Следовать за последней подтверждённой позицией игрока |
| Кнопка «⋯» карты | Открыть выбор территории и целей |

Горячие клавиши меняются в разделе **«Настройки»** кнопкой «Применить». Размер окон меняется за нижний правый угол. Чтобы нажимать кнопки HUD и видеть подсказки карты, отключите режим «Клики в игру».

### Обновление и сохранения

Полностью закройте старую версию через **«Выход»**, затем запустите новый установщик и выберите прежнюю папку. После запуска трекера снова войдите в персонажа.

- Программа: `%LOCALAPPDATA%\Programs\Aion2SoulTracker` — постоянная папка программ пользователя, не временная.
- Настройки и прогресс: `%LOCALAPPDATA%\Aion2SoulTracker` — хранятся отдельно и не удаляются установщиком при обновлении или удалении программы.
- Удаление: «Установленные приложения» Windows → Aion 2 Soul Tracker.

### Частые вопросы

**Почему написано «нужен вход»?** При старте нужна начальная коллекция. После подтверждённой потери данных тоже требуется повторный вход в персонажа. Временная пауза сама по себе не должна сбрасывать синхронизацию. Начиная с 0.3.1 улучшены переключение сетевых адаптеров, обработка смены сервера и восстановление захвата. Это не гарантирует восстановление счётчика после настоящей потери данных.

**Души считаются, но цели не добавляются автоматически?** Проверьте галочку «Добавлять в HUD при получении души». Автодобавление выключено по умолчанию, не работает в данжах и не добавляет уже завершённые цели. В режиме фокуса видимый список определяется фокусом.

**Почему нет точек или отстаёт игрок?** Координаты известны не для всех монстров: сейчас есть места появления для 160 из 200 обычных питомцев. Позиция игрока обновляется по подтверждённым игровым сообщениям и может отставать; её возраст указан на карте. Точки показывают возможные места появления, а не наличие живого монстра.

**Почему после крестика приложение осталось запущено?** Главное окно сворачивается в трей. Для полного закрытия используйте «Выход» слева или в меню значка трея.

### Обратная связь

[Сообщить об ошибке или предложить улучшение](https://github.com/WasyaPetechkin/Aion2SoulTracker/issues). Напишите версию программы, локацию, что ожидали, что произошло и как повторить. Скриншот поможет; перед отправкой скройте имя персонажа. Не публикуйте сетевые записи и всю папку сохранений. При проблемах запуска полезен `startup-error.txt` из папки данных.

---

## English

**Aion 2 Soul Tracker** helps you collect souls and choose your next farming target. The catalogue tracks your collection, a compact HUD shows souls remaining to MAX, and a separate interactive map displays known monster spawn locations.

### Download and install

**[Download the Windows installer 0.3.5](https://github.com/WasyaPetechkin/Aion2SoulTracker/releases/download/v0.3.5/Aion2SoulTracker-Setup-v0.3.5.exe)** · [All versions and files](https://github.com/WasyaPetechkin/Aion2SoulTracker/releases)

1. Run `Aion2SoulTracker-Setup-v0.3.5.exe`, install the application and open its shortcut.
2. If prompted for **Npcap**, install it from the [official website](https://npcap.com/#download) with the default options, then click “Check again”. An existing Npcap installation usually does not need reinstalling.
3. Log in to your character **after starting the tracker**. If already playing, return to character selection and log in again so the tracker receives your initial collection.
4. Enable your categories and pin targets in the **HUD** column. Use windowed or borderless game mode for the HUD.

**.NET is bundled** — no Python, SDK or development tools are needed. Npcap is installed separately. This test build is unsigned; Windows may display an unknown publisher warning.

| File under Releases → Assets | Purpose |
| --- | --- |
| `Aion2SoulTracker-Setup-v0.3.5.exe` | Standard installation — recommended |
| `Aion2SoulTracker-v0.3.5-portable-win-x64.zip` | Extract the entire archive to a separate folder and run the EXE; Npcap is still required |
| `Aion2SoulTracker-v0.3.5-source.zip` | Source code for developers |
| `SHA256SUMS-v0.3.5.txt` | File checksums |

### LagoFast and other accelerators

Version 0.3.5 fixes capture through the LagoFast local relay. The tracker automatically detects its port and uses Loopback. Character login and soul counter updates have been verified with LagoFast. Start the tracker before logging in to your character; fully exit the old version before updating.

Other accelerators using a similar relay may work but have not been tested separately.

### Features

| Feature | How it works |
| --- | --- |
| Catalogue | Russian and English names, name/ID search, categories, owned and MAX filters |
| Pinned targets | Always at the top, even outside the current search or filters |
| HUD | Portrait, name, territory, progress and souls remaining; each row’s cross removes a manual target; it is hidden in focus mode |
| Map | Six territories, zooming, panning, spawn points and the last confirmed player position |
| Map tooltips | Hover a point for its name and portrait; nearby points are grouped |
| Kibelisks | 162 locations across Altgard, Morheim, Verteron and Eltnen; excluded from the Abyss |
| Focus | Selects targets closest to MAX in the player’s current location for both HUD and map |
| Auto-pinning | When enabled, a newly received soul adds its incomplete target to the HUD; dungeons are excluded |
| Settings | RU/EN, editable hotkeys, background opacity and independent HUD row scale of 65–150% |
| Tray and help | Closing the main window hides it to the tray; “?” badges explain settings |

### Target selection and focus

- **No manual targets and focus is off:** the map automatically shows all incomplete targets in enabled categories.
- **Manual targets are pinned:** the map narrows to them; categories still apply. Unpin through the catalogue or the cross on a HUD row.
- **Focus is on:** eligible monsters in the current player location are ranked by souls remaining to MAX. If two need 5 souls, both are shown. Once one needs 4, it becomes the sole target; reaching MAX advances to the next.
- Manual pins are preserved while focus is active. If the player location is unknown, focus waits for it rather than selecting targets from another territory.
- **Enable “Add to HUD when a soul is received” separately.** It reacts to new gains in known open-world locations. Character login, loading saved progress and dungeon gains do not add targets.
- Search, “Hide MAX” and “Owned only” filter the catalogue; pinned targets remain visible. Categories also affect the map and focus.

### Territories in the catalogue

The **Territory** column shows known spawn territories: Asmodians, Elyos or Abyss. Hover it for location names. Completed monsters retain this information: disable **Hide MAX**, then search by name or ID. There is no need to add them to the HUD.

The **Territory** filter above search affects only the catalogue. Manually pinned targets remain visible. HUD, map and focus are unaffected. “Territory unknown” means missing data.

### Settings directly in the HUD

Click **“HUD settings ▾”** at the bottom of the counter. It provides focus, automatic soul tracking, categories, map visibility and click through. The “Collection filters” submenu contains special pets, “Hide MAX” and “Owned only”. Changes immediately synchronize with the main window. Categories affect the map and focus; collection filters affect only the main catalogue.

Menu tooltips wrap and close when the pointer leaves, an item is selected or the menu closes.

If the menu does not respond, disable click through with `Ctrl+Alt+L` (or your custom shortcut). Row crosses are hidden in focus mode because targets are selected automatically. Turn focus off to return to manual targets.

### Controls

| Default | Action |
| --- | --- |
| `Ctrl+Alt+H` | Show / hide HUD |
| `Ctrl+Alt+M` | Show / hide map |
| `Ctrl+Alt+L` | Pass clicks through to the game / enable window controls |
| Mouse wheel over the map | Zoom |
| Drag the map | Pan |
| “Me” button | Follow the last confirmed player position |
| Map “⋯” button | Open territory and target selection |

Change hotkeys in **Settings**, then click “Apply”. Resize windows using their bottom-right corners. Disable “Click through” to use HUD buttons and map tooltips.

### Updates and saved data

Fully close the old version using **Exit**, run the new installer and select the same folder. After starting the tracker, log in to your character again.

- Application: `%LOCALAPPDATA%\Programs\Aion2SoulTracker` — a permanent per-user application folder, not a temporary folder.
- Settings and progress: `%LOCALAPPDATA%\Aion2SoulTracker` — stored separately and not removed by the installer during upgrades or uninstalling.
- Uninstall through Windows Settings → Installed apps → Aion 2 Soul Tracker.

### Frequently asked questions

**Why does it say “login required”?** Starting capture requires an initial collection. Confirmed data loss also requires another character login. A temporary pause alone should not reset synchronization. Improvements introduced in 0.3.1 cover network adapter transitions, server transfers and capture recovery. This does not guarantee counter recovery after actual data loss.

**Souls are counted, but targets are not added automatically?** Check “Add to HUD when a soul is received”. It is off by default, excludes dungeons and does not add completed targets. While focus is active, focus determines which targets are visible.

**Why are points missing or the player marker delayed?** Some monsters have no known coordinates: the dataset currently covers 160 of 200 regular pets. The player marker uses confirmed game messages and can lag; its age is shown on the map. Spawn points indicate possible locations, not currently living monsters.

**Why is the app still running after clicking X?** The main window hides to the tray. Use Exit in the sidebar or tray menu to quit completely.

### Feedback

[Report a bug or suggest an improvement](https://github.com/WasyaPetechkin/Aion2SoulTracker/issues). Include the app version, location, expected and actual behaviour, and reproduction steps. Screenshots help; hide your character name first. Do not publish network recordings or your entire saved-data folder. For startup problems, `startup-error.txt` in the data folder may help.

---

## Скриншоты / Screenshots

Меню настроек HUD / HUD settings menu:

![HUD settings](https://raw.githubusercontent.com/WasyaPetechkin/Aion2SoulTracker/fix/hud-controls-v0.3.2/docs/images/hud-menu.png)

Демонстрационные данные / Demonstration data.

![Collection / Коллекция](https://raw.githubusercontent.com/WasyaPetechkin/Aion2SoulTracker/f94568c87c70902fb26f855d502caa07e8786c86/docs/images/collection.png)
<p><img src="https://raw.githubusercontent.com/WasyaPetechkin/Aion2SoulTracker/f94568c87c70902fb26f855d502caa07e8786c86/docs/images/hud.png" width="310" alt="HUD"> <img src="https://raw.githubusercontent.com/WasyaPetechkin/Aion2SoulTracker/f94568c87c70902fb26f855d502caa07e8786c86/docs/images/map.png" width="310" alt="Map"></p>

Скриншоты предыдущего оформления; портреты и новые элементы 0.3.5 описаны выше. / Screenshots show the earlier layout; portraits and new 0.3.5 controls are described above.

## Исходный код и сборка / Source and build

Код 0.3.5: [исходный код релиза / release source](https://github.com/WasyaPetechkin/Aion2SoulTracker/tree/v0.3.5). Главная страница содержит описание проекта; для сборки используйте указанный коммит или архив `Aion2SoulTracker-v0.3.5-source.zip`. / The main page contains project information; to build, use the linked commit or `Aion2SoulTracker-v0.3.5-source.zip`.

Для сборки нужны .NET 10 SDK и Inno Setup 7. / Building requires .NET 10 SDK and Inno Setup 7.

```powershell
git clone https://github.com/WasyaPetechkin/Aion2SoulTracker.git
cd Aion2SoulTracker
git checkout v0.3.5
dotnet test tests/SoulTracker.Tests -c Release
dotnet build src/SoulTracker.App -c Release
./build.ps1
```

Результаты находятся в `dist/`. Можно передать `-DotnetPath` и `-IsccPath`, если инструменты не в PATH. / Outputs are in `dist/`. Use `-DotnetPath` and `-IsccPath` if the tools are not on PATH.

## Лицензия и благодарности / License and credits

**GPL-3.0.** Независимый фанатский проект, не официальный продукт NCSOFT. / An independent fan project, not an official NCSOFT product.

- Захват и разбор пакетов / Capture and parsing: [aion2-dps-meter](https://github.com/cyberbadger6969/aion2-dps-meter).
- Карты, данные и портреты / Maps, data and portraits: [aion2-nyerk](https://github.com/xCROv/aion2-nyerk), [Questlog](https://questlog.gg/aion-2/en/map).
- Игровые изображения, названия и данные принадлежат соответствующим правообладателям. / Game artwork, names and data belong to their respective owners.
- [Лицензия / License](https://github.com/WasyaPetechkin/Aion2SoulTracker/blob/v0.3.5/LICENSE) · [Сторонние компоненты / Third-party notices](https://github.com/WasyaPetechkin/Aion2SoulTracker/blob/v0.3.5/licenses/THIRD-PARTY.md).

Тестовая версия для Global EU; обновления игры могут потребовать обновления трекера. / A test version for Global EU; game updates may require tracker updates.
