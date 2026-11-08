# Compressed bundle integrity — 0.3.8

## Русский

Продолжение пункта E аудита после C1–C3 версии 0.3.7. В исходном аудите частичная обработка повреждённого bundle была вопросом для проверки. Теперь дефект воспроизведён синтетическими тестами. Подтверждения эксплуатации, утечки данных или связи с конкретным сбоем VPN нет.

### Подтверждённый сценарий

`FrameDecoder.WalkInner` раньше немедленно вызывал обработчик каждого внутреннего сообщения. Поэтому корректный префикс с начислением успевал изменить коллекцию до обнаружения обрезанного хвоста. При ошибке вложенного сжатого блока внешний цикл продолжал обрабатывать следующие сообщения. Идентификация и снимок коллекции после ошибочного блока могли вернуть доверие к данным из того же повреждённого контейнера.

Отдельно кадр с необязательным флагом, но без полного двухбайтового opcode, молча пропускался без `DataLost`. Три регрессионных теста были запущены до исправления и завершились ошибкой: соответственно отправлено одно сообщение, отправлено три сообщения и отсутствует сигнал потери.

### Изменение

`FrameDecoder.HandleFrame` создаёт временную группу для внешнего сжатого блока. `DecodeBundle` и `WalkInner` проверяют все длины, вложенные блоки LZ4 и заголовки, сохраняя только ссылки на уже распакованные данные. Вызовы обработчиков начинаются после успешной проверки всей структуры и сохраняют исходный порядок. При ошибке ни одно внутреннее сообщение этой группы не отправляется, а владельцу потока передаётся один сигнал потери.

Буферы остаются в группе до окончания вызовов обработчиков и возвращаются в `ArrayPool` через `finally`, в том числе при исключении. Данные распаковываются один раз; несжатые сообщения используют прежний прямой путь.

Ограничения на один внешний bundle:

| Ограничение | Значение |
| --- | --- |
| Размер одного распакованного блока | 8 МиБ, как ранее |
| Сумма всех распаковок, включая вложенные | 32 МиБ |
| Число сжатых узлов, включая внешний | 1024 |
| Число внутренних кадров, включая вложенные контейнеры | 65 536 |
| Глубина сжатия | Уровни 0–4, как ранее |

Это ограничение объёма распаковки, а не обещание общего потребления памяти ровно 32 МиБ: пул может выделять более крупные массивы, также существуют служебные списки. Пределы ограничивают суммарную работу по одному контейнеру. Обычные корректные пакеты не запускают дополнительных сетевых запросов, фоновых опросов или таймеров.

`Wire.TryVarint` теперь отклоняет пятый байт с битами за пределами uint32. Раньше эти биты отбрасывались сдвигом и могли превращать переполненную длину в небольшое число. Полный диапазон 0…uint.MaxValue остаётся допустимым; при отказе позиция чтения не меняется.

### Проверки и границы результата

- `BundleIntegrityTests`: повреждённый хвост и вложенный блок, неполный opcode с флагом, корректные вложения/порядок/содержимое/дробление, каждый общий лимит, переполнение varint и максимальное допустимое значение.
- Интеграционный тест через `PacketPipeline` и `CollectionSession`: повреждённая группа не меняет число душ и не восстанавливает доверие; следующая отдельная корректная группа со снимком и идентификацией восстанавливает его и учитывает ровно одно начисление.
- Все 124 теста ядра проходят, включая 14 новых случаев и прежние 180 детерминированных вариантов доставки TCP.
- На четырёх собственных игровых записях число сообщений осталось прежним: 18 120 / 20 507 / 15 101 / 1 763 632. Сообщений получения душ: 1 / 5 / 2 / 0. Итоговые счётчики и состояние синхронизации совпали с результатом до изменения. Последняя запись по-прежнему правильно отмечена недостоверной из-за реальных пропусков TCP.

Проверяется структура и сжатие контейнера. Это не транзакционный откат произвольных исключений в обработчиках и не новый парсер семантики всех игровых сообщений. Исключения обработчика по-прежнему обрабатываются защитой сессии из 0.3.7. Не изменены эвристики поиска встроенной идентичности в `PacketParser`, источники координат, правила повышения уровня и сетевой маршрут. Реальные PCAP в дистрибутив не входят; новые зависимости не добавлены.

## English

This addresses audit item E after the C1–C3 changes in 0.3.7. Three tests reproduced partial dispatch from a malformed compressed tree and a missing loss notification for an incomplete flagged opcode. A bad nested block previously allowed later sibling identity/snapshot messages to restore trust inside the same damaged container.

`FrameDecoder` now validates and stages the complete compressed tree before dispatching any contained packet. Payloads reference pooled buffers retained until dispatch finishes and returned in `finally`. Decoding happens once, order is preserved, and uncompressed packets keep their direct path. A structural failure rejects the whole group and reports loss to its owning stream.

Per outer bundle, expansion is capped at 32 MiB, compressed nodes at 1024 and inner frames at 65,536; the existing 8 MiB per-node and depth 0–4 limits remain. These bound decompression work, not total process memory. `Wire.TryVarint` also rejects overflow in the fifth uint32 byte without advancing its cursor.

All 124 core tests pass. Four private capture replays retain exactly the previous message totals, soul counts and synchronization state. No new dependencies or private captures are distributed. Live exploitation or a link to previous VPN incidents has not been established. Structural validation does not provide rollback of arbitrary downstream handler exceptions or change the semantic parser of every game message; existing stream-loss handling still applies.
