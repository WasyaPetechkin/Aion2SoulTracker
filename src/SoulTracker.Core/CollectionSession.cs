using AionMeter.Core.Capture;
namespace SoulTracker.Core;

/// <summary>Owns live collection trust. A new stream needs both identity and a full snapshot.
/// Neither a matching character name nor an identical gain proves transport continuity.</summary>
public sealed class CollectionSession(CollectionState state,Action<IReadOnlyList<SoulGain>,int?> gained,Action<string,string> log,Action changed)
{
    private readonly object _gate=new();
    private readonly Dictionary<long,Candidate> _candidates=[];
    private readonly Dictionary<int,long> _retiredOwners=[];
    private readonly HashSet<long> _retiredStreams=[];
    private Candidate? _active;
    private readonly NavigationState _idleNavigation=new();
    private long _retiredThrough,_maxGeneration,_lastAccepted;
    private int? _owner;
    private bool _replay;
    public long Epoch {get;private set;}
    public NavigationState Navigation {get {lock(_gate)return _active?.Navigation ?? _idleNavigation;}}
    public StreamContext? ActiveStream {get {lock(_gate)return _active?.Context;}}
    public int PendingStreams {get {lock(_gate)return _candidates.Count;}}
    private sealed class Candidate(StreamContext context)
    {
        public StreamContext Context=context;
        public NavigationState Navigation=new();
        public string Name="";
        public int Server;
        public bool FreshIdentity;
        public long IdentityTime;
        public bool RequiresSnapshot=true;
        public byte[]? Snapshot;
        public long SnapshotTime;
        public List<(long Time,byte[] Body)> Gains=[];
        public int GainBytes;
    }
    private Candidate? Get(StreamContext context)
    {
        _maxGeneration=Math.Max(_maxGeneration,context.Generation);
        if(_retiredStreams.Contains(context.Generation) || context.Generation<=_retiredOwners.GetValueOrDefault(context.ProcessId) ||
           (context.Generation<=_retiredThrough && _active?.Context!=context) || (_owner.HasValue && context.ProcessId!=_owner))return null;
        if(_candidates.TryGetValue(context.Generation,out var c))return c.Context==context ? c : null;
        // Pending streams are bounded; forgotten generations cannot later become authoritative.
        if(_candidates.Count>=32)
        {
            var oldest=_candidates.Values.Where(x=>x!=_active).MinBy(x=>x.Context.Generation)!;
            _candidates.Remove(oldest.Context.Generation);
            _retiredThrough=Math.Max(_retiredThrough,oldest.Context.Generation);
        }
        c=new(context);c.Navigation.Reset(_replay);_candidates[context.Generation]=c;return c;
    }
    public void ObserveProcesses(IReadOnlyCollection<int> pids)
    {
        lock(_gate)
        {
            if(_owner is not >0 || pids.Contains(_owner.Value))return;
            _retiredOwners[_owner.Value]=_maxGeneration;
            while(_retiredOwners.Count>64)
            {
                var oldest=_retiredOwners.MinBy(x=>x.Value);_retiredOwners.Remove(oldest.Key);
                _retiredThrough=Math.Max(_retiredThrough,oldest.Value);
            }
            _owner=null;_active=null;_candidates.Clear();_idleNavigation.Reset(_replay);changed();
            state.MarkUncertain("Владелец игровой сессии завершён; ожидается новая коллекция");
        }
    }
    public void Reset(string reason,bool replay=false)
    {
        lock(_gate)
        {
            _replay=replay;
            _retiredThrough=_maxGeneration;_owner=null;_active=null;_candidates.Clear();
            _idleNavigation.Reset(replay);changed();state.MarkUncertain(reason);
        }
    }
    public void ObserveLoss(StreamLoss loss)
    {
        lock(_gate)
        {
            var c=Get(loss.Stream);if(c is null)return;
            c.Snapshot=null;c.Gains.Clear();c.GainBytes=0;c.FreshIdentity=false;c.RequiresSnapshot=true;c.Navigation.MarkGap();
            log("stream-loss",$"generation={loss.Stream.Generation}; pid={loss.Stream.ProcessId}; {loss.Evidence.Reason}; seq={loss.Evidence.Sequence}..{loss.Evidence.EndSequence}");
            if(c==_active)state.MarkUncertain("Пропущен участок сети; ожидается новая синхронизация персонажа");
        }
    }
    public void Identify(StreamContext context,long time,string name,int server)
    {
        if(string.IsNullOrWhiteSpace(name))return;
        lock(_gate)
        {
            var c=Get(context);if(c is null || time<_lastAccepted)return;
            if(server==0 && c.Name==name)server=c.Server;
            var different=c.Name.Length>0 && (c.Name!=name || (c.Server>0 && server>0 && c.Server!=server));
            c.Name=name;c.Server=server;c.FreshIdentity=true;c.IdentityTime=time;
            if(c!=_active)
            {
                // A pre-opened connection needs a recent snapshot before promotion.
                if(context.StartedMs<_lastAccepted && c.SnapshotTime<_lastAccepted)return;
                if(_active is {} previous)
                {
                    _retiredStreams.Add(previous.Context.Generation);_candidates.Remove(previous.Context.Generation);
                    while(_retiredStreams.Count>64)
                    {var oldest=_retiredStreams.Min();_retiredStreams.Remove(oldest);_retiredThrough=Math.Max(_retiredThrough,oldest);}
                }
                _owner=context.ProcessId;_active=c;Epoch++;changed();
                state.MarkUncertain("Новое соединение: ожидается полная коллекция");
                log("session",$"epoch={Epoch}; generation={context.Generation}; pid={context.ProcessId}");
            }
            else if(different){Epoch++;changed();c.RequiresSnapshot=true;state.MarkUncertain("Смена персонажа: ожидается полная коллекция");}
            state.Identify(time,name,server);
            Commit(c);
        }
    }
    public bool Process(StreamContext context,long time,ushort opcode,ReadOnlySpan<byte> body)
    {
        lock(_gate)
        {
            var c=Get(context);if(c is null)return false;
            c.Navigation.Process(time,opcode,body);
            if(opcode is not (PetProtocol.SnapshotOpcode or PetProtocol.GainOpcode))return true;
            if(time<_lastAccepted)
            {
                if(c==_active && opcode==PetProtocol.GainOpcode)Invalidate(c,"Soul gain timestamp moved backwards");
                return false;
            }
            if(opcode==PetProtocol.SnapshotOpcode)
            {
                if(!PetProtocol.TrySnapshot(time,body,out _)){Invalidate(c,"Invalid collection snapshot");return false;}
                if(c==_active && !c.RequiresSnapshot)c.FreshIdentity=false;
                c.Snapshot=body.ToArray();c.SnapshotTime=time;c.Gains.Clear();c.GainBytes=0;
                if(_active is not null)state.MarkUncertain("Получена коллекция; ожидается подтверждение персонажа");
                if(c!=_active && c.FreshIdentity && c.IdentityTime>=_lastAccepted)
                    Identify(c.Context,c.IdentityTime,c.Name,c.Server);
                Commit(c);return true;
            }
            if(!PetProtocol.TryGains(body,out var gains)){Invalidate(c,"Invalid soul gain");return false;}
            if(c.Snapshot is not null)
            {
                if(c.Gains.Count>=4096 || c.GainBytes+body.Length>1024*1024){Invalidate(c,"Pending collection gains overflow");return false;}
                c.Gains.Add((time,body.ToArray()));c.GainBytes+=body.Length;return true;
            }
            if(c!=_active)
            {
                if(_active is not null)state.MarkUncertain("Новое соединение без снимка коллекции; требуется синхронизация");
                return false;
            }
            if(!state.Process(time,opcode,body))
            {
                if(state.IsTrusted)Invalidate(c,"Soul gain could not be applied atomically");
                return false;
            }
            _lastAccepted=Math.Max(_lastAccepted,time);gained(gains,c.Navigation.View().WorldId);return true;
        }
    }
    private void Invalidate(Candidate c,string reason)=>ObserveLoss(new(c.Context,new(c.Context.Flow,_lastAccepted,reason,ProcessId:c.Context.ProcessId)));
    private void Commit(Candidate c)
    {
        if(c!=_active || !c.FreshIdentity || c.Snapshot is null)return;
        if(c.SnapshotTime<_lastAccepted){c.Snapshot=null;c.Gains.Clear();c.GainBytes=0;return;}
        if(!state.Process(c.SnapshotTime,PetProtocol.SnapshotOpcode,c.Snapshot))return;
        c.Snapshot=null;c.FreshIdentity=false;c.RequiresSnapshot=false;_lastAccepted=c.SnapshotTime;
        state.SetSource("Живые данные игры");log("snapshot",$"Collection synchronized; epoch={Epoch}; generation={c.Context.Generation}");
        var buffered=c.Gains.ToArray();c.Gains.Clear();c.GainBytes=0;
        foreach(var gain in buffered)
        {
            if(!state.Process(gain.Time,PetProtocol.GainOpcode,gain.Body))
            {Invalidate(c,"Buffered soul gain could not be applied atomically");break;}
            _lastAccepted=Math.Max(_lastAccepted,gain.Time);
            if(PetProtocol.TryGains(gain.Body,out var parsed))gained(parsed,c.Navigation.View().WorldId);
        }
    }
}
