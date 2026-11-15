# Диагностика / Diagnostics — 0.3.9

## Русский

Кнопка **«Диагностика»** в главном окне открывает состояние цепочки от процесса игры до принятых начислений душ. Окно обновляется раз в секунду, пока открыто состояние подключения. «Подготовить отчёт» фиксирует снимок и показывает точный JSON, который будет сохранён или скопирован. Возврат «К состоянию» возобновляет обновление. Закрытие окна останавливает его таймер; сам трекер продолжает работать.

### Состав отчёта

Формат `SchemaVersion: 1` определён в [DiagnosticReport.cs](../src/SoulTracker.Core/DiagnosticReport.cs). Поля перечислены явно; это не копия `status.json` с попыткой скрыть адреса.

| Группа | Содержимое |
| --- | --- |
| Общие сведения | Версия приложения, время создания UTC, режим Live / Offline / Replay, наличие библиотеки Npcap и итоговое состояние |
| Захват | Число процессов и соединений в последней проверке, число сетевых и Loopback-читателей, счётчики пакетов, сегментов, распознанных потоков, пропусков и ошибок; возраст проверки и последнего игрового трафика |
| Коллекция | Наличие сохранённых значений, доверие к текущему счёту, подтверждение персонажа, ожидающие снимки; возраст последнего принятого снимка, начисления и потери выбранного потока |
| Начисления | Число принятых сообщений начисления и суммарное количество душ для выбранной сессии |
| Последние события | Не более 20 событий за последние 30 минут: только тип и возраст, без свободного текста |

Имена персонажей и серверов, ID процессов, адреса и порты соединений, имена адаптеров и компьютера, пути, координаты, список питомцев, содержимое пакетов и тексты исключений не включаются. Причины потерь и ошибок переводятся в ограниченный набор кодов. Неизвестная причина становится `Other`, её исходный текст не копируется.

Время создания отчёта остаётся абсолютным временем UTC; остальные отметки передаются как возраст в секундах. Отчёт ничего не отправляет и не сохраняет автоматически. Копирование меняет буфер обмена только после нажатия соответствующей кнопки. Обычные локальные журналы и файлы сохранений не становятся обезличенными от появления этой функции.

### Как трактовать результат

- `Ready` означает, что коллекция подтверждена и недавно наблюдался распознанный игровой трафик. Это не обещание отсутствия ошибок игры или VPN.
- `WaitingForTraffic` сохраняет доверие к коллекции: обычная пауза не создаёт потерю и не требует релог сама по себе.
- `AwaitingCollection` и `AwaitingIdentity` отделяют сохранённый прогресс и ожидающий снимок от подтверждённой текущей коллекции.
- `NeedsResync` означает, что непрерывность счёта выбранной сессии не подтверждена. Причина последней потери показана отдельно как история; после восстановления она может оставаться в отчёте.
- Отсутствие принятых начислений само по себе не считается ошибкой: игрок мог не получать души после входа.

Счётчики захвата могут включать другие потоки; их ненулевое значение не переводит исправную коллекцию в `NeedsResync`. Начисления относятся к текущей выбранной сессии и сбрасываются при её смене или сбросе. Ожидающие или отклонённые начисления не выдаются за принятые. Число распознанных потоков не доказывает, что каждый из них активен сейчас; для свежести отдельно показан возраст трафика.

### Реализация и границы

[LiveCapture.Diagnostics](../src/AionMeter.Core/Capture/LiveCapture.cs) читает существующие счётчики под блокировками захвата. [CollectionSession.Diagnostics](../src/SoulTracker.Core/CollectionSession.cs) возвращает состояние выбранной сессии. [TrackerSession.Diagnostics](../src/SoulTracker.Core/TrackerSession.cs) собирает отчёт, а [DiagnosticsWindow](../src/SoulTracker.App/DiagnosticsWindow.xaml.cs) управляет просмотром и явным экспортом. Окно не запускает второй захват, не проверяет маршрут активными запросами, не перезаходит в игру и не изменяет прогресс.

Отчёт помогает локализовать этап сбоя, но не восстанавливает пропущенные души и не устанавливает причину внешнего обрыва связи. Состояния относятся к моменту создания снимка; отчёт лучше готовить, пока проблема ещё проявляется.

## English

**Diagnostics** in the main window shows the chain from the game process to accepted soul gains. Connection status refreshes once per second while visible. **Prepare report** freezes a snapshot and displays the exact JSON that Save or Copy will use. **Back to status** resumes updates. Closing the window stops its timer; the tracker continues running.

### Report contents

`SchemaVersion: 1` is defined in [DiagnosticReport.cs](../src/SoulTracker.Core/DiagnosticReport.cs). Fields are explicitly allowlisted; this is not a copy of `status.json` with addresses redacted.

| Group | Contents |
| --- | --- |
| General | App version, UTC creation time, Live / Offline / Replay mode, Npcap library presence and overall outcome |
| Capture | Last scanned process and connection counts, network and Loopback reader counts, packet, segment, recognized-flow, gap and error totals; scan and last game-traffic ages |
| Collection | Stored-value presence, current counter trust, character confirmation, pending snapshots; ages of the latest accepted snapshot, gain and selected-stream loss |
| Gains | Accepted gain-message count and total soul amount for the selected session |
| Recent events | At most 20 events from the last 30 minutes: type and age only, with no free text |

Character and server names, process IDs, connection addresses and ports, adapter and machine names, file paths, coordinates, the pet list, packet contents and exception messages are excluded. Loss and error reasons are mapped to a limited set of codes. Unknown reasons become `Other`; the original text is not copied.

Creation time remains an absolute UTC timestamp; other timestamps become ages in seconds. The report is neither sent nor saved automatically. Copy changes the clipboard only when clicked. Existing local logs and saved-data files are not anonymized by this feature.

### Interpreting the result

- `Ready` means the collection is confirmed and recognized game traffic was observed recently. It does not guarantee error-free game or VPN operation.
- `WaitingForTraffic` preserves collection trust: an ordinary pause does not create a loss or require another login by itself.
- `AwaitingCollection` and `AwaitingIdentity` distinguish saved progress and pending snapshots from a confirmed current collection.
- `NeedsResync` means the selected session's counter continuity is not confirmed. The last loss reason is historical and may remain in a report after recovery.
- No accepted gains alone is not an error: the player may not have received a soul since login.

Capture totals may include other streams; nonzero totals do not turn a healthy collection into `NeedsResync`. Gains belong to the current selected session and reset when it changes or is reset. Pending or rejected gains are not reported as accepted. A recognized-flow count does not prove every flow remains active; traffic age is shown separately.

### Implementation and limits

[LiveCapture.Diagnostics](../src/AionMeter.Core/Capture/LiveCapture.cs) reads existing counters under capture locks. [CollectionSession.Diagnostics](../src/SoulTracker.Core/CollectionSession.cs) supplies selected-session state. [TrackerSession.Diagnostics](../src/SoulTracker.Core/TrackerSession.cs) assembles the report, and [DiagnosticsWindow](../src/SoulTracker.App/DiagnosticsWindow.xaml.cs) handles preview and explicit export. The window does not start a second capture, actively probe the route, log back into the game or change progress.

The report helps identify the affected stage. It cannot recover missing souls or establish the cause of an external connection outage. States describe the snapshot time, so prepare a report while the problem is still occurring.
