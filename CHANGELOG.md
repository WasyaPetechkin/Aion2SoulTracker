# 0.3.3 — 2026-10-21

Исправлена читаемость текста в меню настроек HUD и вложенных пунктах.

Fixed HUD settings menu and submenu text contrast.

# 0.3.2 — 2026-10-21

Исправлен белый квадрат в строках режима фокуса. В HUD добавлено меню настроек: фокус, автодобавление душ, категории, карта, клики в игру и фильтры каталога. Настройки синхронизируются с главным окном. Сетевой захват не изменён относительно 0.3.1.

Fixed the white square in focus rows. Added a HUD settings menu for focus, automatic soul tracking, categories, map visibility, click through and collection filters, synchronized with the main window. Network capture is unchanged from 0.3.1.

# 0.3.1 — 2026-10-21

Тестовое исправление сетевого захвата: одновременное чтение старого и нового адаптера при смене маршрута; стабильный входящий фильтр игрового порта при смене сервера; обновление списка VPN-адаптеров; повторный запуск остановленного захвата. Исправлено распознавание разбитых между TCP-сегментами heartbeat-сообщений, повторное использование TCP-соединения и сохранение частично прочитанного кадра при сетевой паузе. Добавлена диагностика причин потери синхронизации и ошибок чтения душ. HUD показывает ожидание сети. Требуется длительная проверка с ClearVPN в игре.

Network capture test fix: overlapping adapter capture during route changes, stable inbound game-port filters during server transfers, refreshed VPN adapter discovery and recovery of stopped capture. Fixed fragmented heartbeat detection, reused TCP connection handling and partial-frame retention during quiet periods. Added bounded synchronization-loss diagnostics and explicit unreadable soul-gain reporting. HUD indicates when it is waiting for network traffic. Extended in-game ClearVPN verification is still required.

# 0.3.0 — 2026-10-20

Quiet connections preserve synchronization; late TCP retransmissions wait up to five seconds before a confirmed gap. Capture status reads use a consistent flow snapshot. Actual data loss still requires synchronization.

Automatic category fallback, local focus, pinned-first catalogue, numeric sorting, per-row HUD removal, optional open-world soul auto-pinning, pet portraits, spawn tooltips and 162 faction-zone kibelisks.

# Изменения

## 0.2.0

- Боковая навигация: коллекция, настройки программы и выход.
- Подсказки «?» у настроек, фильтров и кнопок.
- Настраиваемые горячие клавиши с проверкой конфликтов Windows.
- Закрытие главного окна крестиком сворачивает его в трей.
- Режим фокуса синхронно выбирает ближайших к MAX в счётчике и на карте.
  При равенстве показывает всех лидеров; после MAX переходит к следующему.
- Более компактные строки HUD и независимый масштаб 65–150%.
- Категории сохраняются между запусками.
- Подготовлена необязательная Authenticode-подпись при сборке. Тестовая сборка не подписана.

## 0.1.1

- Без отмеченных целей HUD карта показывает всех незавершённых из выбранных категорий.
- Выбор мобов сужает карту; снятие последней отметки возвращает категории.
- Текстовый поиск меняет только каталог.

## 0.1.0

- Каталог коллекции, счётчик прогресса и интерактивная карта.
- Русский и английский интерфейс, сохранение прогресса.
- Установщик Windows x64 со встроенной средой .NET.
