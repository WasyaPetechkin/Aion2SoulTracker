namespace AionMeter.Core.Capture;

/// <summary>Port filters capture a new connection before the next process-table poll.
/// Buffer it briefly, but decode only endpoints owned by the game process.</summary>
public sealed class GameConnectionGate(Action<SegmentReader.Segment> deliver)
{
    private readonly Dictionary<FlowKey,long> _known=[];
    private readonly Queue<SegmentReader.Segment> _pending=[];
    private int _bytes;
    public int PendingCount=>_pending.Count;
    public long Discarded {get;private set;}
    public void Update(IEnumerable<GameConnection> connections,long now)
    {
        foreach(var c in connections)_known[new(c.RemoteIp,c.RemotePort,c.LocalIp,c.LocalPort)]=now+30_000;
        foreach(var key in _known.Where(x=>x.Value<now).Select(x=>x.Key).ToArray())_known.Remove(key);
        var count=_pending.Count;
        for(var i=0;i<count;i++)
        {
            var segment=_pending.Dequeue();_bytes-=segment.Payload.Length;
            if(_known.ContainsKey(segment.Key))deliver(segment);
            else if(now-segment.TimeMs<=10_000){_pending.Enqueue(segment);_bytes+=segment.Payload.Length;}
            else Discarded++;
        }
    }
    public void Push(SegmentReader.Segment segment)
    {
        if(_known.TryGetValue(segment.Key,out var expiry) && expiry>=segment.TimeMs){deliver(segment);return;}
        _pending.Enqueue(segment);_bytes+=segment.Payload.Length;
        while(_bytes>2*1024*1024 || _pending.Count>4096)
        {_bytes-=_pending.Dequeue().Payload.Length;Discarded++;}
    }
    public void Clear(){_known.Clear();_pending.Clear();_bytes=0;}
}
