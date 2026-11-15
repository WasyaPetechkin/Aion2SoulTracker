using System.Windows.Media;
using AionMeter.Core.Capture;
using SoulTracker.Core;
namespace SoulTracker.App;

internal sealed record DiagnosticStep(string Title,string Status,string Detail,int Tone)
{
    public Brush Color=>new SolidColorBrush(Tone switch{1=>System.Windows.Media.Color.FromRgb(141,224,206),2=>System.Windows.Media.Color.FromRgb(237,200,120),_=>System.Windows.Media.Color.FromRgb(168,186,206)});
}
internal sealed class DiagnosticText(string language)
{
    public string T(string ru,string en)=>language=="ru"?ru:en;
    private string Age(long? age)=>age is null ? T("ещё не было","not yet") : age<60 ? T($"{age} с назад",$"{age}s ago") : age<3600 ? T($"{age/60} мин назад",$"{age/60}m ago") : T($"{age/3600} ч назад",$"{age/3600}h ago");
    public (string Title,string Hint) Outcome(DiagnosticOutcome code)=>code switch
    {
        DiagnosticOutcome.Ready=>(T("Чтение работает · коллекция синхронизирована","Capture working · collection synchronized"),T("Отсутствие новых начислений само по себе не означает сбой. Для проверки получите душу незавершённого питомца.","No new gains alone does not indicate a fault. To check counting, obtain a soul for an incomplete pet.")),
        DiagnosticOutcome.WaitingForTraffic=>(T("Ожидаются данные игры","Waiting for game traffic"),T("Последняя коллекция сохраняет доверие. Обычная сетевая пауза не требует релога. Если игра активна, проверьте подключение или VPN.","The last collection remains trusted. An ordinary pause does not require another login. If the game is active, check its connection or VPN.")),
        DiagnosticOutcome.NpcapMissing=>(T("Npcap не готов к чтению игры","Npcap is not ready for capture"),T("Установите Npcap с официального сайта и нажмите «Проверить снова» в главном окне. Если он уже установлен, перезапустите трекер.","Install Npcap from its official site and click Check again in the main window. If already installed, restart the tracker.")),
        DiagnosticOutcome.Starting=>(T("Запускается чтение игры","Starting game capture"),T("Подождите несколько секунд: трекер проверяет процесс и соединения игры.","Wait a few seconds while the tracker checks the game process and connections.")),
        DiagnosticOutcome.GameNotFound=>(T("Игра не найдена","Game not found"),T("Запустите Aion 2. После запуска трекера войдите в персонажа.","Start Aion 2, then log in to your character after starting the tracker.")),
        DiagnosticOutcome.ConnectionNotFound=>(T("Ожидается соединение игры","Waiting for the game connection"),T("Процесс найден, но подходящее соединение пока не обнаружено. Войдите в игру; если ожидание сохраняется, сохраните отчёт с включённым ускорителем или VPN.","The process was found, but a suitable connection has not been detected. Log in; if this persists, save a report with your accelerator or VPN still enabled.")),
        DiagnosticOutcome.CaptureUnavailable=>(T("Чтение сети не запущено","Network capture is not running"),T("Проверьте Npcap и перезапустите трекер. Если проблема повторяется, сохраните этот отчёт.","Check Npcap and restart the tracker. If the issue returns, save this report.")),
        DiagnosticOutcome.ProtocolNotRecognized=>(T("Игровые сообщения ещё не распознаны","Game messages not recognized yet"),T("Захват открыт. Подождите после входа в персонажа. Если состояние не меняется, сохраните отчёт; по нему можно проверить этап распознавания.","Capture is open. Wait after character login. If this does not change, save a report to help check protocol recognition.")),
        DiagnosticOutcome.AwaitingIdentity=>(T("Коллекция получена · ожидается персонаж","Collection received · waiting for character confirmation"),T("Трекер ждёт подтверждения, кому принадлежит коллекция. Если ожидание сохраняется, вернитесь к выбору персонажа и войдите снова.","The tracker is waiting to confirm which character owns the collection. If this persists, return to character selection and log in again.")),
        DiagnosticOutcome.AwaitingCollection=>(T("Ожидается полная коллекция","Waiting for a complete collection"),T("Войдите в персонажа после запуска трекера. Сохранённый прогресс не подтверждает актуальность данных этой сессии.","Log in to your character after starting the tracker. Saved progress does not establish this session's live state.")),
        DiagnosticOutcome.NeedsResync=>(T("Нужна повторная синхронизация","Synchronization is required"),T("Непрерывность счёта не подтверждена. Вернитесь к выбору персонажа и войдите снова при запущенном трекере.","Counter continuity is not confirmed. Return to character selection and log in again with the tracker running.")),
        DiagnosticOutcome.Replay=>(T("Открыта запись игрового обмена","Reading a recorded game session"),T("Это данные записи, а не проверка текущего подключения игры.","This is recorded data, not a check of the current game connection.")),
        _=>(T("Просмотр сохранённых данных","Viewing saved data"),T("Живой захват выключен. Сохранённая коллекция не означает, что трекер сейчас читает игру.","Live capture is off. A saved collection does not mean the tracker is currently reading the game."))
    };
    public IReadOnlyList<DiagnosticStep> Steps(DiagnosticReport report)
    {
        var c=report.Capture;var s=report.Collection;var live=report.Mode==DiagnosticMode.Live;var checking=T("Ожидание","Waiting");var unavailable=T("Не проверяется","Not checked");
        return [
            new(T("1. Процесс игры","1. Game process"),!live?unavailable:c?.Processes>0?T("Найден","Found"):checking,
                T($"Процессов: {c?.Processes??0}",$"Processes: {c?.Processes??0}"),live&&c?.Processes>0?1:0),
            new(T("2. Соединение","2. Connection"),!live?unavailable:c?.Connections>0||c?.RecentTraffic==true?T("Обнаружено","Detected"):checking,
                T($"Соединений в последней проверке: {c?.Connections??0}; через локальный посредник: {c?.RelayConnections??0}",$"Connections in the last scan: {c?.Connections??0}; local relay: {c?.RelayConnections??0}"),live&&(c?.Connections>0||c?.RecentTraffic==true)?1:0),
            new(T("3. Захват сети","3. Network capture"),!live?unavailable:c?.ActiveReaders>0?T("Открыт","Open"):checking,
                T($"Npcap: {(report.NpcapInstalled?"найден":"не найден")}. Сетевых интерфейсов: {c?.NetworkReaders??0}; Loopback: {c?.LoopbackReaders??0}",$"Npcap: {(report.NpcapInstalled?"found":"not found")}. Network readers: {c?.NetworkReaders??0}; Loopback: {c?.LoopbackReaders??0}"),live&&c?.ActiveReaders>0?1:0),
            new(T("4. Протокол игры","4. Game protocol"),!live?unavailable:c?.RecognizedFlows>0?T("Распознан","Recognized"):checking,
                T("Последний игровой трафик: ","Last game traffic: ")+Age(c?.TrafficAgeSeconds),live&&c?.RecentTraffic==true?1:0),
            new(T("5. Коллекция","5. Collection"),s.SnapshotAgeSeconds is not null?T("Получена из обмена","Received from game traffic"):s.PendingSnapshots>0?T("Ждёт подтверждения персонажа","Awaiting character confirmation"):s.HasStoredValues?T("Только сохранённые значения","Saved values only"):checking,
                T("Последний подтверждённый снимок: ","Last confirmed snapshot: ")+Age(s.SnapshotAgeSeconds),live&&s.SnapshotAgeSeconds is not null?1:0),
            new(T("6. Непрерывность","6. Continuity"),live&&s.Trusted&&s.SnapshotAgeSeconds is not null?T("Подтверждена","Confirmed"):T("Не подтверждена","Not confirmed"),
                T("Последняя потеря выбранного потока: ","Last selected-stream loss: ")+Age(s.LossAgeSeconds),live&&s.Trusted&&s.SnapshotAgeSeconds is not null?1:report.Outcome==DiagnosticOutcome.NeedsResync?2:0),
            new(T("7. Начисления душ","7. Soul gains"),s.GainAgeSeconds is not null?T("Были приняты","Gains accepted"):T("Ещё не было · это не ошибка","None yet · not an error"),
                T($"Последнее: {Age(s.GainAgeSeconds)}; событий: {s.AcceptedGainMessages}; душ: {s.AcceptedSoulAmount}",$"Last: {Age(s.GainAgeSeconds)}; events: {s.AcceptedGainMessages}; souls: {s.AcceptedSoulAmount}"),live&&s.GainAgeSeconds is not null&&s.Trusted?1:0)
        ];
    }
    public string Counters(DiagnosticReport d)
    {
        var c=d.Capture;var route=d.Collection.Route switch{DiagnosticRoute.Network=>T("сеть","network"),DiagnosticRoute.Loopback=>"Loopback",_=>T("не определён","not determined")};
        return T($"Выбранный поток: {route}. С запуска захвата: пакетов {c?.CapturedPackets??0}, сообщений {d.DecodedMessages}, пропусков TCP {c?.TcpGaps??0}, ошибок захвата {c?.Errors??0}. Отклонено сообщений коллекции: {d.RejectedCollectionMessages}.",
            $"Selected stream: {route}. Since capture start: packets {c?.CapturedPackets??0}, messages {d.DecodedMessages}, TCP gaps {c?.TcpGaps??0}, capture errors {c?.Errors??0}. Rejected collection messages: {d.RejectedCollectionMessages}.")+
            T(" Начисления выше — для текущей выбранной сессии. Счётчики потерь захвата могут включать другие потоки."," Gains above belong to the current selected session. Capture loss totals can include other streams.");
    }
    public string Issue(DiagnosticReport d)
    {
        var loss=d.Collection.LastLoss switch
        {
            StreamIssue.TcpGap=>T("пропущен участок TCP","a TCP range was missed"),StreamIssue.OwnershipBuffer=>T("потеря в буфере до подтверждения владельца","ownership buffer loss"),
            StreamIssue.PrelockBuffer=>T("потеря до распознавания потока","loss before stream recognition"),StreamIssue.Framing=>T("ошибка структуры сообщения","message framing error"),
            StreamIssue.InvalidSnapshot=>T("нечитаемая коллекция","unreadable collection"),StreamIssue.InvalidGain=>T("непринятое начисление","rejected gain"),
            StreamIssue.HandlerFailure=>T("ошибка обработки сообщения","message handler failure"),StreamIssue.ConnectionRestart=>T("перезапуск соединения","connection restart"),
            StreamIssue.CaptureFailure=>T("остановка захвата из-за ошибки","capture stopped after an error"),StreamIssue.Capacity=>T("превышен лимит буфера","buffer capacity exceeded"),
            StreamIssue.Other=>T("другая потеря непрерывности","other continuity loss"),_=>""
        };
        var warning=d.Capture?.State==CaptureState.Error ? T("Захват сообщает об ошибке. Рабочий поток может продолжать доставлять данные. ","Capture reports an error; a working stream may still deliver data. "):"";
        return warning+(loss.Length==0?"":T("Последняя причина потери: ","Last loss reason: ")+loss+T(". Это история; текущий результат указан выше.",". This is historical; the current result is shown above."));
    }
}
