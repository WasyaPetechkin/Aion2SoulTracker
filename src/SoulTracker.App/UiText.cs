using System.Text.RegularExpressions;
using SoulTracker.Core;

namespace SoulTracker.App;
internal sealed class UiText(string language)
{
    public bool Russian => language=="ru";
    public string Choose(string ru,string en) => Russian ? ru : en;
    public string Title => Choose("Коллекция душ","Soul collection");
    public string Subtitle => Choose("Aion 2 · Global EU · версия 0.3.2","Aion 2 · Global EU · version 0.3.2");
    public string Summary(CollectionView view)
    {
        if(!view.HasSnapshot) return Choose("Ожидается коллекция из игры","Waiting for the game collection");
        var regular=view.Pets.Where(x=>x.IsRegular).ToArray();
        var owned=regular.Count(x=>x.Level>0);
        var max=regular.Count(x=>x.Completed);
        return Choose($"Получено {owned} / {regular.Length}    ·    MAX {max}    ·    Осталось завершить {regular.Length-max}",
            $"Owned {owned} / {regular.Length}    ·    MAX {max}    ·    Still to complete {regular.Length-max}");
    }
    public string Source(string value) => value switch
    {
        "Запись" => Choose("Сохранённая запись","Saved recording"),
        "Запись игрового обмена" => Choose("Сохранённая запись игры","Recorded game session"),
        "Живые данные игры" => Choose("Живые данные игры","Live game data"),
        "Сохранённая запись; ожидается синхронизация при входе" =>
            Choose("Сохранённая запись; ожидается вход в персонажа","Saved recording; waiting for character login"),
        _ => value
    };
    public string Status(string status)
    {
        if(status.StartsWith("Capturing",StringComparison.Ordinal))
            return Choose("Чтение игры активно","Reading game data") + (status.Contains("REC",StringComparison.Ordinal)
                ? Choose(" · запись включена"," · recording enabled") : "");
        if(status.StartsWith("Listening on",StringComparison.Ordinal)) return Choose("Ожидается игровой обмен","Waiting for game traffic");
        if(status.StartsWith("Replay ready",StringComparison.Ordinal)) return Choose("Запись готова к чтению","Recording ready");
        if(status.StartsWith("Replaying",StringComparison.Ordinal)) return Choose("Чтение сохранённой записи…","Reading saved recording…");
        if(status.StartsWith("Replay finished",StringComparison.Ordinal)) return Choose("Запись прочитана","Recording read");
        if(status.StartsWith("Capture error:",StringComparison.Ordinal)) return Choose("Ошибка чтения: ","Capture error: ")+status[14..].Trim();
        if(status.StartsWith("No Npcap adapter",StringComparison.Ordinal)) return Choose("Сетевой адаптер игры не найден","Game network adapter not found");
        if(status.StartsWith("Npcap is not installed",StringComparison.Ordinal)) return Choose("Npcap не установлен","Npcap is not installed");
        if(status.StartsWith("Запись прочитана; пропусков потока: ",StringComparison.Ordinal))
            return Choose("Запись прочитана; пропусков потока: ","Recording read; stream gaps: ")+status.Split(':').Last().Trim();
        return status switch
        {
            "Загрузка записи…" => Choose("Загрузка записи…","Loading recording…"),
            "Сохранённый прогресс восстановлен" => Choose("Сохранённый прогресс восстановлен","Saved progress restored"),
            "Capture stopped" => Choose("Чтение остановлено","Capture stopped"),
            "Starting capture…" => Choose("Запуск чтения игры…","Starting game capture…"),
            "AION 2 is not running" => Choose("Aion 2 не запущен","Aion 2 is not running"),
            "Waiting for the game to connect…" => Choose("Ожидается подключение игры…","Waiting for the game to connect…"),
            "Connected · no game traffic for a while" => Choose("Подключено; ожидаются данные игры","Connected; waiting for game data"),
            "Selected network adapter not found" => Choose("Выбранный сетевой адаптер не найден","Selected network adapter not found"),
            _ => status
        };
    }
    public string History(string value,IReadOnlyList<PetProgress> pets)
    {
        var snapshot=Regex.Match(value,@"^(\d\d:\d\d:\d\d\s+)Коллекция: (\d+) питомца, (\d+) счётчиков душ$");
        if(snapshot.Success) return snapshot.Groups[1].Value + Choose(
            $"Коллекция: питомцев — {snapshot.Groups[2].Value}, счётчиков душ — {snapshot.Groups[3].Value}",
            $"Collection: {snapshot.Groups[2].Value} pets, {snapshot.Groups[3].Value} soul counters");
        if(Russian)
        {
            foreach(var pet in pets)
            {
                var prefix=pet.Name+": ";
                if(value.Length>=10 && value.AsSpan(10).StartsWith(prefix,StringComparison.Ordinal))
                    return value[..10]+PetText.Name(pet,language)+value[(10+pet.Name.Length)..];
            }
            return value;
        }
        value=value.Replace(": уровень ",": level ",StringComparison.Ordinal);
        var messages=new Dictionary<string,string>
        {
            ["Не удалось прочитать получение души; нужен новый вход в персонажа"]="Could not decode a soul gain; log in again to synchronize",
                ["Начато прослушивание: для актуальной коллекции нужен новый вход в персонажа"]="Capture started: log in again to sync the collection",
            ["В записи есть пропуски; требуется новая синхронизация"]="The recording has gaps; a new sync is required",
            ["Пропущен участок сети; нужен новый вход в персонажа"]="Network data was missed; log in again to sync",
            ["Соединение прервано; после входа ожидается новая коллекция"]="Connection interrupted; waiting for a new collection after login"
        };
        foreach(var pair in messages) value=value.Replace(pair.Key,pair.Value,StringComparison.Ordinal);
        return value;
    }
}
