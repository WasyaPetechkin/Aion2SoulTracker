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
    public CollectionSession Streams {get;}
    public NavigationState Navigation=>Streams.Navigation;
    public string Status { get; private set; } = "Capture stopped";
    public bool LiveSnapshotReceived { get; private set; }
    public long ObservedPackets { get; private set; }
    public long RejectedPetMessages { get; private set; }
    public CaptureHealth? Health=>_live?.Health();
    private readonly GameData _data;
    private LiveCapture? _live;
    private PacketPipeline? _inputPipeline;
    public sealed record NetworkEvent(DateTimeOffset Time,string Kind,string Detail);
    private readonly ConcurrentQueue<NetworkEvent> _networkEvents=new();
    public NetworkEvent[] NetworkEvents=>_networkEvents.ToArray();
    public string[] CaptureAdapters=>_live?.ActiveAdapters ?? [];
    public long CaptureErrors=>_live?.CaptureErrors ?? 0;
    private CaptureState? _lastCaptureState;
    private void LogNetwork(string kind,string detail)
    {if(_networkEvents.LastOrDefault() is { } last && last.Kind==kind && last.Detail==detail)return;_networkEvents.Enqueue(new(DateTimeOffset.UtcNow,kind,detail));while(_networkEvents.Count>100)_networkEvents.TryDequeue(out _);}
    private bool _hasConnected;

    public TrackerSession(IReadOnlyList<PetDefinition> catalog, GameData data)
    {
        State = new(catalog);
        _data = data;
        Streams=new(State,QueueGains,LogNetwork,()=>_receivedSouls.Clear());
    }
    public void Replay(string path)
    {
        Streams.Reset("Открыта запись игрового обмена",replay:true);
        Navigation.Reset(replay:true);
        using var replay = new PcapReplaySource(_data, path, false);
        BindPipeline(replay.Pipeline);
        replay.EventDecoded += HandleEvent;
        replay.RunToEnd();
        State.SetSource("Запись игрового обмена");
        Status = $"Запись прочитана; пропусков потока: {replay.Pipeline.Gaps}";
        UpdateLiveTrust();
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
        Streams.Reset("Начат захват новой игровой сессии");
        State.SetSource("Сохранённая запись; ожидается синхронизация при входе");
        State.MarkUncertain("Начато прослушивание: для актуальной коллекции нужен новый вход в персонажа");
        _live = new LiveCapture(_data, new LiveCaptureOptions { RecordDirectory = recordDirectory });
        BindPipeline(_live.Pipeline);
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
        if(_lastCaptureState!=status.State){LogNetwork("capture",status.State+": "+status.Message);_lastCaptureState=status.State;}
        if(status.State==CaptureState.Capturing)_hasConnected=true;
        // A quiet connection or a temporary absence from the TCP table is not lost data.
        if(_hasConnected && status.State==CaptureState.WaitingForGame)
        {
            Streams.Reset("Соединение прервано; после входа ожидается новая коллекция");
            LiveSnapshotReceived=false;_hasConnected=false;
        }
    }
    // Synthetic/offline verification entry point. Live and replay messages always use BindPipeline.
    public bool ProcessSyntheticPetMessage(long time,ushort op,ReadOnlySpan<byte> body)
    {
        if(_inputPipeline is not null || _live is not null)throw new InvalidOperationException("Synthetic input is not permitted during capture or replay");
        var wellFormed=op==PetProtocol.SnapshotOpcode ? PetProtocol.TrySnapshot(time,body,out _) : op==PetProtocol.GainOpcode && PetProtocol.TryGains(body,out _);
        if(!wellFormed)
        {
            LogNetwork("invalid-pet-message",$"opcode={op:X4}; bytes={body.Length}");
            if(op==PetProtocol.GainOpcode){State.MarkUncertain("Не удалось прочитать получение души; нужен новый вход в персонажа");LiveSnapshotReceived=false;}
            return false;
        }
        if(!State.Process(time,op,body)){LogNetwork("ignored-pet-message",$"opcode={op:X4}; synchronization required");return false;}
        if(op==PetProtocol.SnapshotOpcode)LogNetwork("snapshot","Collection synchronized");
        if(op==PetProtocol.GainOpcode && PetProtocol.TryGains(body,out var gains))
        {
            QueueGains(gains,Navigation.View().WorldId);
        }
        return true;
    }
    private void QueueGains(IReadOnlyList<SoulGain> gains,int? world)
    {foreach(var gain in gains)_receivedSouls.Enqueue(new(gain.PetId,world));while(_receivedSouls.Count>4096)_receivedSouls.TryDequeue(out _);}
    private void UpdateLiveTrust()=>LiveSnapshotReceived=State.IsTrusted;
    private void BindPipeline(PacketPipeline pipeline)
    {
        _inputPipeline=pipeline;
        pipeline.Parser.Tap=(time,op,body)=>
        {
            ObservedPackets++;
            if(pipeline.CurrentContext is not {} context)return;
            if(!Streams.Process(context,time,op,body) && op is PetProtocol.SnapshotOpcode or PetProtocol.GainOpcode)RejectedPetMessages++;
            UpdateLiveTrust();
        };
        pipeline.LossObserved+=loss=>{Streams.ObserveLoss(loss);UpdateLiveTrust();};
        pipeline.ProcessesObserved+=pids=>{Streams.ObserveProcesses(pids);UpdateLiveTrust();};
    }
    private void HandleEvent(GameEvent ev)
    {
        if(ev is SelfIdentifiedEvent self && _inputPipeline?.CurrentContext is {} context)
        {Streams.Identify(context,self.TimeMs,self.Name,self.ServerId);UpdateLiveTrust();}
    }
    public void Dispose() => _live?.Dispose();
}
