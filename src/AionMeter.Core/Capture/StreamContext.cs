namespace AionMeter.Core.Capture;

/// <summary>A TCP tuple can be reused. Generation identifies this particular incarnation.</summary>
public sealed record StreamContext(FlowKey Flow,long Generation,int ProcessId,long StartedMs);
public sealed record LossEvidence(FlowKey Flow,long TimeMs,string Reason,uint? Sequence=null,uint? EndSequence=null,int ProcessId=0);
public sealed record StreamLoss(StreamContext Stream,LossEvidence Evidence);
