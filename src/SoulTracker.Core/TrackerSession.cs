using System.Text.Json;
using System.Collections.Concurrent;
using AionMeter.Core.Capture;
using AionMeter.Core.Events;
using AionMeter.Core.Game;

namespace SoulTracker.Core;

public sealed class TrackerSession : IDisposable
{
    public sealed record ReceivedSoul(int PetId,int? WorldId);
    private readonly ConcurrentQueue<ReceivedSoul> _receivedSouls=new();
    public IReadOnlyList<ReceivedSoul> DrainReceivedSouls()
    {var result=new List<ReceivedSoul>();while(_receivedSouls.TryDequeue(out var gain))result.Add(gain);return result;}
    public CollectionState State { get; }
    public NavigationState Navigation {get;}=new();
    public string Status { get; private set; } = "Capture stopped";
    public bool LiveSnapshotReceived { get; private set; }
    public long ObservedPackets { get; private set; }
    public long RejectedPetMessages { get; private set; }
    public CaptureHealth? Health=>_live?.Health();
    private readonly GameData _data;
    private LiveCapture? _live;
    private long _gaps;
    private bool _hasConnected;

    public TrackerSession(IReadOnlyList<PetDefinition> catalog, GameData data)
    {
        State = new(catalog);
        _data = data;
    }
    public void Replay(string path)
    {
        Navigation.Reset(replay:true);
        using var replay = new PcapReplaySource(_data, path, false);
        replay.Pipeline.Parser.Tap = (time,op,body) =>
        {
            ObservedPackets++;
            Navigation.Process(time,op,body);
            if (op is PetProtocol.SnapshotOpcode or PetProtocol.GainOpcode)
            {
                if (!State.Process(time,op,body)) RejectedPetMessages++;
            }
        };
        replay.EventDecoded += HandleEvent;
        replay.RunToEnd();
        State.SetSource("Запись игрового обмена");
        Status = $"Запись прочитана; пропусков потока: {replay.Pipeline.Gaps}";
        if (replay.Pipeline.Gaps > 0) State.MarkUncertain("В записи есть пропуски; требуется новая синхронизация");
    }
    public bool Restore(string path)
    {
        try
        {
            if(!File.Exists(path)) return false;
            var saved=JsonSerializer.Deserialize<CollectionView>(File.ReadAllText(path));
            if(saved is null || !State.Restore(saved)) return false;
            Status="Сохранённый прогресс восстановлен";
            return true;
        }
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        { return false; }
    }
    public void StartLive(string? recordDirectory = null)
    {
        Navigation.Reset();
        State.SetSource("Сохранённая запись; ожидается синхронизация при входе");
        State.MarkUncertain("Начато прослушивание: для актуальной коллекции нужен новый вход в персонажа");
        _live = new LiveCapture(_data, new LiveCaptureOptions { RecordDirectory = recordDirectory });
        _live.Pipeline.Parser.Tap = (time,op,body) =>
        {
            if (_live.Pipeline.Gaps != _gaps)
            {
                _gaps = _live.Pipeline.Gaps;
                Navigation.MarkGap();
                State.MarkUncertain("Пропущен участок сети; нужен новый вход в персонажа");
                LiveSnapshotReceived = false;
            }
            Navigation.Process(time,op,body);
            if (op is not (PetProtocol.SnapshotOpcode or PetProtocol.GainOpcode)) return;
            if (!ProcessLivePetMessage(time,op,body)) { RejectedPetMessages++; return; }
            if (op == PetProtocol.SnapshotOpcode)
            {
                LiveSnapshotReceived = true;
                State.SetSource("Живые данные игры");
            }
        };
        _live.EventDecoded += HandleEvent;
        _live.StatusChanged += ObserveCaptureStatus;
        _live.Start();
    }
    public void Save(string path)
    {
        var view = State.View();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(view, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }
    public void ObserveCaptureStatus(CaptureStatus status)
    {
        Status=status.Message;
        if(status.State==CaptureState.Capturing)_hasConnected=true;
        // A quiet connection or a temporary absence from the TCP table is not lost data.
        if(_hasConnected && status.State is CaptureState.Error or CaptureState.WaitingForGame)
        {
            Navigation.Reset();
            State.MarkUncertain("Соединение прервано; после входа ожидается новая коллекция");
            LiveSnapshotReceived=false;_hasConnected=false;
        }
    }
    public bool ProcessLivePetMessage(long time,ushort op,ReadOnlySpan<byte> body)
    {
        if(!State.Process(time,op,body))return false;
        if(op==PetProtocol.GainOpcode && PetProtocol.TryGains(body,out var gains))
        {
            foreach(var gain in gains)_receivedSouls.Enqueue(new(gain.PetId,Navigation.View().WorldId));
            while(_receivedSouls.Count>4096)_receivedSouls.TryDequeue(out _);
        }
        return true;
    }
    private void HandleEvent(GameEvent ev)
    {
        if (ev is SelfIdentifiedEvent self) State.Identify(self.TimeMs,self.Name,self.ServerId);
    }
    public void Dispose() => _live?.Dispose();
}
