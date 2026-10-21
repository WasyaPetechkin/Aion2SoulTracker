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
