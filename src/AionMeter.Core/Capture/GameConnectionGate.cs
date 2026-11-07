namespace AionMeter.Core.Capture;

/// <summary>Decode only process-owned endpoints; retain bounded loss evidence until ownership is known.</summary>
public sealed class GameConnectionGate(Action<SegmentReader.Segment> deliver)
{
    private readonly Dictionary<FlowKey,(long Expiry,int Pid)> _known=[];
    private readonly Queue<SegmentReader.Segment> _pending=[];
    private readonly Dictionary<FlowKey,LossEvidence> _losses=[];
    private int _bytes;
    public int PendingCount=>_pending.Count;
    public int PendingLossCount=>_losses.Count;
    public int CurrentProcessId {get;private set;}
    public long Discarded {get;private set;}
    public event Action<LossEvidence>? DataLost;
    public void ReportFailure(SegmentReader.Segment segment,string reason)
    {
        Discard(segment,reason);
        if(_known.TryGetValue(segment.Key,out var owner) && _losses.Remove(segment.Key,out var loss))
            DataLost?.Invoke(loss with {ProcessId=owner.Pid});
    }
    private void Discard(SegmentReader.Segment s,string reason)
    {
        Discarded++;
        // Multiple discarded ranges may be disjoint or from a reused tuple: do not
        // pretend that the last range describes all the lost bytes.
        var bounded=!s.Syn && s.Payload.Length>0 && !_losses.ContainsKey(s.Key);
        _losses[s.Key]=new(s.Key,s.TimeMs,reason,bounded?s.Seq:null,bounded?unchecked(s.Seq+(uint)s.Payload.Length):null);
        // This journal contains metadata only, never payloads from unowned processes.
        while(_losses.Count>512)_losses.Remove(_losses.MinBy(x=>x.Value.TimeMs).Key);
    }
    private void Deliver(SegmentReader.Segment s,int pid)
    {
        CurrentProcessId=pid;
        try
        {
            if(_losses.Remove(s.Key,out var loss))DataLost?.Invoke(loss with {ProcessId=pid});
            deliver(s);
        }
        finally {CurrentProcessId=0;}
    }
    public void Update(IEnumerable<GameConnection> connections,long now)
    {
        foreach(var c in connections)_known[new(c.RemoteIp,c.RemotePort,c.LocalIp,c.LocalPort)]=(now+30_000,c.ProcessId);
        foreach(var key in _known.Where(x=>x.Value.Expiry<now).Select(x=>x.Key).ToArray())_known.Remove(key);
        // Report confirmed losses even if no buffered segment survived.
        foreach(var key in _losses.Keys.Where(_known.ContainsKey).ToArray())
        {var loss=_losses[key];_losses.Remove(key);DataLost?.Invoke(loss with {ProcessId=_known[key].Pid});}
        var count=_pending.Count;
        for(var i=0;i<count;i++)
        {
            var segment=_pending.Dequeue();_bytes-=segment.Payload.Length;
            if(_known.TryGetValue(segment.Key,out var owner))Deliver(segment,owner.Pid);
            else if(now-segment.TimeMs<=10_000){_pending.Enqueue(segment);_bytes+=segment.Payload.Length;}
            else Discard(segment,"Ownership buffer expired");
        }
    }
    public void Push(SegmentReader.Segment segment)
    {
        if(segment.Payload.Length==0 && !segment.Syn && !segment.FinOrRst)return;
        if(_known.TryGetValue(segment.Key,out var owner) && owner.Expiry>=segment.TimeMs){Deliver(segment,owner.Pid);return;}
        _pending.Enqueue(segment);_bytes+=segment.Payload.Length;
        while(_bytes>2*1024*1024 || _pending.Count>4096)
        {var removed=_pending.Dequeue();_bytes-=removed.Payload.Length;Discard(removed,"Ownership buffer full");}
    }
    public void Clear(){_known.Clear();_pending.Clear();_losses.Clear();_bytes=0;}
}
