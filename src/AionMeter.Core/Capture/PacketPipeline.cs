using System.Net;
using AionMeter.Core.Events;
using AionMeter.Core.Game;
using AionMeter.Core.Protocol;

namespace AionMeter.Core.Capture;

public readonly record struct FlowKey(IPAddress Src, ushort SrcPort, IPAddress Dst, ushort DstPort)
{
    public override string ToString() => $"{Src}:{SrcPort} → {Dst}:{DstPort}";
}

/// <summary>
/// TCP segments in, game events out. Every server→client flow is watched for the heartbeat frame
/// (<c>0E 00 36</c>, ~19/s). Once a flow shows enough of them it is locked as a game stream and everything buffered
/// so far is replayed through reassembly → framing → parsing, so records sent right at connect (our own character)
/// are not lost. TLS flows on the same server are recognised and ignored.
/// </summary>
public sealed class PacketPipeline
{
    public const int GamePort = 13328;
    private const int PreLockBufferBytes = 512 * 1024;
    private const int FlowIdleMs = 120_000;

    private static readonly byte[][] HeartbeatSignatures = [[0x0E, 0x00, 0x36], [0x06, 0x00, 0x36]];

    private readonly Dictionary<FlowKey, Flow> _flows = new();
    private readonly PacketParser _parser;
    private long _lastPruneMs;
    private static long _generation;
    private readonly Dictionary<FlowKey,LossEvidence> _pendingLoss=[];

    public PacketPipeline(GameData data, Action<GameEvent> sink)
    {
        _parser = new PacketParser(data, sink);
    }

    public PacketParser Parser => _parser;

    /// <summary>Heartbeats needed before a flow is trusted. Lower when the capture is already filtered to the game.</summary>
    public int LockThreshold { get; set; } = 6;

    public long Segments { get; private set; }
    public long GameBytes { get; private set; }
    public int GameFlows => _flows.Values.Count(f => f.IsGame);
    public IEnumerable<FlowKey> LockedFlows => _flows.Values.Where(f => f.IsGame).Select(f => f.Key);
    /// <summary>Holes in the game streams skipped since start (lost packets): each costs whatever the hole held.</summary>
    public long Gaps { get; private set; }
    public long LastGameDataMs { get; private set; }

    public event Action<FlowKey>? FlowLocked;
    public event Action<FlowKey,string>? StreamDiscontinuity;
    public FlowKey? CurrentFlow {get;private set;}
    public StreamContext? CurrentContext {get;private set;}
    public event Action<StreamLoss>? LossObserved;
    public event Action<int[]>? ProcessesObserved;
    public void ObserveProcesses(int[] pids)=>ProcessesObserved?.Invoke(pids);
    public void InvalidateAll(string reason)
    {foreach(var f in _flows.Values.Where(x=>x.IsGame))ReportLoss(f,new(f.Key,f.LastSeenMs,reason,ProcessId:f.Context.ProcessId));}
    public void ReportCaptureLoss(LossEvidence loss)
    {
        if(_flows.TryGetValue(loss.Flow,out var f) && f.Context.ProcessId==loss.ProcessId)
        {
            if(f.IsGame && loss.Sequence is uint start && loss.EndSequence is uint end && unchecked((int)(end-start))>0)
            {
                if(f.Reassembler!.AlreadyDelivered(start,end))return;
                // A known missing range can still arrive through retransmission or another
                // capture adapter. TCP will hold later bytes; do not invalidate prematurely.
                if(f.CaptureLosses.Count<64){f.CaptureLosses.TryAdd((start,end),loss);return;}
            }
            if(f.IsGame)ReportLoss(f,loss);else f.InitialLoss=loss;
        }
        else
        {
            _pendingLoss[loss.Flow]=loss;
            while(_pendingLoss.Count>512)_pendingLoss.Remove(_pendingLoss.MinBy(x=>x.Value.TimeMs).Key);
        }
    }
    private void ReportLoss(Flow flow,LossEvidence loss)
    {
        StreamDiscontinuity?.Invoke(flow.Key,loss.Reason);
        LossObserved?.Invoke(new(flow.Context,loss));
    }
    private Flow NewFlow(FlowKey key,int pid,long time)
    {
        var flow=new Flow(key,new(key,Interlocked.Increment(ref _generation),pid,time)){LastSeenMs=time};
        if(_pendingLoss.Remove(key,out var loss) && loss.ProcessId==pid)flow.InitialLoss=loss;
        return flow;
    }

    public void OnSegment(long timeMs, FlowKey key, uint seq, bool syn, bool finOrRst, ReadOnlySpan<byte> payload,int processId=0)
    {
        Segments++;
        if (timeMs - _lastPruneMs > 10_000) Prune(timeMs);

        if (!_flows.TryGetValue(key, out var flow) || flow.Context.ProcessId!=processId)
        {
            if (payload.IsEmpty && !syn) return;
            if(flow is {IsGame:true})ReportLoss(flow,new(key,timeMs,"TCP owner changed",ProcessId:flow.Context.ProcessId));
            if(_flows.Count>=512 && !_flows.ContainsKey(key))
            {
                var oldest=_flows.MinBy(x=>x.Value.LastSeenMs);
                if(oldest.Value.IsGame)ReportLoss(oldest.Value,new(oldest.Key,timeMs,"Stream capacity exceeded"));
                _flows.Remove(oldest.Key);
            }
            flow = NewFlow(key,processId,timeMs);
            _flows[key] = flow;
        }
        flow.LastSeenMs = timeMs;

        if (syn)
        {
            if(flow.SynNext!=seq+1)
            {
                if(flow.SynNext.HasValue || flow.IsGame)
                {
                    if(flow.IsGame)ReportLoss(flow,new(key,timeMs,"TCP connection restarted",seq,unchecked(seq+1),processId));
                    flow=NewFlow(key,processId,timeMs);_flows[key]=flow;
                }
                flow.SynNext=unchecked(seq+1);
            }
            return;
        }
        if (finOrRst && payload.IsEmpty)
        {
            // A FIN can arrive before retransmitted data. Keep established stream
            // state so late bytes are not interpreted as a new connection.
            if(!flow.IsGame)_flows.Remove(key);
            return;
        }
        if (payload.IsEmpty || flow.IsTls) return;

        if (!flow.IsGame)
        {
            if (flow.PreLock.Count == 0 && LooksLikeTls(payload))
            {
                flow.IsTls = true;
                return;
            }
            if(flow.Probe is null)
            {
                flow.Probe=new TcpReassembler(data=>
                {
                    var previous=flow.ProbeTail.Length;
                    var joined=new byte[previous+data.Length];flow.ProbeTail.CopyTo(joined,0);data.Span.CopyTo(joined.AsSpan(previous));
                    flow.Heartbeats+=CountHeartbeats(joined,previous);
                    flow.ProbeTail=joined.AsSpan(Math.Max(0,joined.Length-10)).ToArray();
                },()=>{flow.ProbeTail=[];flow.Heartbeats=0;flow.InitialLoss=new(key,timeMs,"Pre-lock sequence gap",ProcessId:processId);});
                flow.Probe.Start(flow.SynNext ?? seq);
            }
            flow.Probe.Push(seq,payload,timeMs);
            flow.PreLock.Add((seq, payload.ToArray(), timeMs));
            flow.PreLockBytes += payload.Length;
            while (flow.PreLockBytes > PreLockBufferBytes && flow.PreLock.Count > 1)
            {
                var dropped=flow.PreLock[0];
                flow.PreLockBytes -= flow.PreLock[0].Data.Length;
                flow.PreLock.RemoveAt(0);
                flow.SynNext = null; // the start of the stream is gone
                flow.InitialLoss=new(key,timeMs,"Pre-lock buffer full",dropped.Seq,unchecked(dropped.Seq+(uint)dropped.Data.Length),processId);
            }

            var threshold = key.SrcPort == GamePort ? Math.Min(3, LockThreshold) : LockThreshold;
            if (flow.Heartbeats < threshold) return;
            Lock(flow);
            return;
        }

        Feed(flow, seq, payload, timeMs);
    }

    private void Lock(Flow flow)
    {
        flow.IsGame = true;
        var first = flow.PreLock[0];
        var aligned = flow.SynNext.HasValue;
        flow.Decoder = new FrameDecoder(Handle, aligned);
        var decoder = flow.Decoder;
        decoder.DataLost+=reason=>ReportLoss(flow,new(flow.Key,flow.LastSeenMs,reason,ProcessId:flow.Context.ProcessId));
        flow.Reassembler = new TcpReassembler(d => decoder.Feed(d.Span), () =>
        {
            Gaps++;
            ReportLoss(flow,new(flow.Key,flow.LastSeenMs,"TCP sequence gap: "+flow.Reassembler!.LastGapReason+$"; missing={flow.Reassembler.LastGapMissingBytes}",flow.Reassembler.LastGapStartSequence,flow.Reassembler.LastGapEndSequence,flow.Context.ProcessId));
            decoder.Reset();
        });
        flow.Reassembler.Start(flow.SynNext ?? first.Seq);
        flow.Probe=null;flow.ProbeTail=[];

        var buffered = flow.PreLock;
        flow.PreLock = new();
        flow.PreLockBytes = 0;
        if(flow.InitialLoss is {} loss)ReportLoss(flow,loss);
        foreach (var (seq, data, time) in buffered) Feed(flow, seq, data, time);
        FlowLocked?.Invoke(flow.Key);
    }

    private void Feed(Flow flow, uint seq, ReadOnlySpan<byte> payload, long timeMs)
    {
        CurrentFlow=flow.Key;CurrentContext=flow.Context;
        _parser.TimeMs = timeMs;
        GameBytes += payload.Length;
        LastGameDataMs = timeMs;
        try {flow.Reassembler!.Push(seq, payload, timeMs);}
        catch(Exception ex) when(ex is not OutOfMemoryException)
        {
            ReportLoss(flow,new(flow.Key,timeMs,"Packet handler failed: "+ex.GetType().Name,seq,unchecked(seq+(uint)payload.Length),flow.Context.ProcessId));
            flow.Decoder!.Reset();
        }
        finally {CurrentFlow=null;CurrentContext=null;}
        foreach(var (range,loss) in flow.CaptureLosses.ToArray())
        {
            if(flow.Reassembler!.AlreadyDelivered(range.Start,range.End))flow.CaptureLosses.Remove(range);
            else if(timeMs-loss.TimeMs>TcpReassembler.GapTimeoutMs)
            {flow.CaptureLosses.Remove(range);ReportLoss(flow,loss);}
        }
    }

    private void Handle(ushort opcode, ReadOnlySpan<byte> body) => _parser.Handle(opcode, body);

    private static int CountHeartbeats(ReadOnlySpan<byte> payload,int previous=0)
    {
        var count = 0;
        foreach (var sig in HeartbeatSignatures)
        {
            var span = payload;var offset=0;
            int i;
            while ((i = span.IndexOf(sig)) >= 0)
            {
                // Known heartbeat frames are 3 or 11 bytes; the next byte must start another frame (or end the segment).
                var end = i + (sig[0]==0x06 ? 3 : 11);
                if (offset+end>previous && (end == span.Length || (end < span.Length && span[end] != 0xFF))) count++;
                offset+=i+3;span = span[(i + 3)..];
            }
        }
        return count;
    }

    private static bool LooksLikeTls(ReadOnlySpan<byte> p) =>
        p.Length >= 5 && p[0] is >= 0x14 and <= 0x17 && p[1] == 0x03 && p[2] <= 0x04;

    private void Prune(long nowMs)
    {
        _lastPruneMs = nowMs;
        List<FlowKey>? dead = null;
        foreach (var (k, f) in _flows)
            if (!f.IsGame && nowMs-f.LastSeenMs>FlowIdleMs) (dead ??= new()).Add(k);
        if (dead is not null)
            foreach (var k in dead)
            {_flows.Remove(k);}
    }

    private sealed class Flow(FlowKey key,StreamContext context)
    {
        public FlowKey Key { get; } = key;
        public StreamContext Context {get;}=context;
        public LossEvidence? InitialLoss;
        public Dictionary<(uint Start,uint End),LossEvidence> CaptureLosses=[];
        public bool IsGame;
        public bool IsTls;
        public int Heartbeats;
        public uint? SynNext;
        public long LastSeenMs;
        public List<(uint Seq, byte[] Data, long TimeMs)> PreLock = new();
        public int PreLockBytes;
        public FrameDecoder? Decoder;
        public TcpReassembler? Reassembler;
        public TcpReassembler? Probe;
        public byte[] ProbeTail=[];
    }
}
