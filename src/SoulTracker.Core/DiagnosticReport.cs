using System.Text.Json;
using System.Text.Json.Serialization;
using AionMeter.Core.Capture;
namespace SoulTracker.Core;

public enum DiagnosticMode { Offline, Live, Replay }
public enum DiagnosticOutcome { Offline, Replay, NpcapMissing, Starting, GameNotFound, ConnectionNotFound, CaptureUnavailable, ProtocolNotRecognized, AwaitingIdentity, AwaitingCollection, NeedsResync, WaitingForTraffic, Ready }
public enum DiagnosticEventKind { CaptureChanged, SessionChanged, CollectionSynchronized, ContinuityLost, MessageRejected, Other }
public sealed record DiagnosticEvent(long AgeSeconds,DiagnosticEventKind Kind);
public sealed record DiagnosticCapture(int Processes,int Connections,int RelayConnections,int NetworkReaders,int LoopbackReaders,
    long? DiscoveryAgeSeconds,long CapturedPackets,long GameSegments,int RecognizedFlows,long? TrafficAgeSeconds,long TcpGaps,
    int PendingOwnershipSegments,long DiscardedBeforeOwnership,long Errors,CaptureState State,CaptureIssue Issue)
{
    [JsonIgnore] public int ActiveReaders=>NetworkReaders+LoopbackReaders;
    [JsonIgnore] public bool RecentTraffic=>TrafficAgeSeconds is >=0 and <10;
}
public sealed record DiagnosticCollection(bool HasStoredValues,bool Trusted,bool IdentityConfirmed,int PendingSnapshots,
    long? SnapshotAgeSeconds,long? GainAgeSeconds,long AcceptedGainMessages,long AcceptedSoulAmount,
    long? LossAgeSeconds,StreamIssue LastLoss,DiagnosticRoute Route);

// This is an allowlist, not a redacted dump of live state. No endpoint, identity,
// adapter description, machine name, payload, file path or exception string is accepted.
public sealed record DiagnosticReport(int SchemaVersion,string AppVersion,DateTimeOffset CreatedAt,DiagnosticMode Mode,
    bool NpcapInstalled,DiagnosticCapture? Capture,DiagnosticCollection Collection,
    long DecodedMessages,long RejectedCollectionMessages,IReadOnlyList<DiagnosticEvent> RecentEvents)
{
    public DiagnosticOutcome Outcome => Mode switch
    {
        DiagnosticMode.Offline=>DiagnosticOutcome.Offline,
        DiagnosticMode.Replay=>DiagnosticOutcome.Replay,
        _=>LiveOutcome()
    };
    private DiagnosticOutcome LiveOutcome()
    {
        if(!NpcapInstalled || Capture?.Issue==CaptureIssue.NpcapUnavailable)return DiagnosticOutcome.NpcapMissing;
        if(Capture is not {} c)return DiagnosticOutcome.CaptureUnavailable;
        if(c.State==CaptureState.Error && c.ActiveReaders==0)return DiagnosticOutcome.CaptureUnavailable;
        if(c.DiscoveryAgeSeconds is null)return DiagnosticOutcome.Starting;
        if(c.Processes==0)return DiagnosticOutcome.GameNotFound;
        if(c.ActiveReaders==0)return c.Connections==0 ? DiagnosticOutcome.ConnectionNotFound : DiagnosticOutcome.CaptureUnavailable;
        if(c.RecognizedFlows==0)return c.Connections==0 ? DiagnosticOutcome.ConnectionNotFound : DiagnosticOutcome.ProtocolNotRecognized;
        if(Collection.SnapshotAgeSeconds is null)return Collection.PendingSnapshots>0 ? DiagnosticOutcome.AwaitingIdentity : DiagnosticOutcome.AwaitingCollection;
        if(!Collection.Trusted)return DiagnosticOutcome.NeedsResync;
        return c.RecentTraffic ? DiagnosticOutcome.Ready : DiagnosticOutcome.WaitingForTraffic;
    }
    public string ToJson()=>JsonSerializer.Serialize(this,JsonOptions);
    private static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}};

    public static DiagnosticReport Create(Version version,DateTimeOffset now,DiagnosticMode mode,bool npcap,
        CaptureDiagnostics? capture,CollectionDiagnostics collection,long decoded,long rejected,
        IEnumerable<TrackerSession.NetworkEvent> events)
    {
        var time=now.ToUnixTimeMilliseconds();
        long? Age(long stamp)=>stamp<=0 ? null : Math.Max(0,(time-stamp)/1000);
        DiagnosticCapture? safe=capture is not {} c ? null : new(c.Processes,c.Connections,c.RelayConnections,c.NetworkReaders,c.LoopbackReaders,
            Age(c.DiscoveryTimeMs),c.CapturedPackets,c.GameSegments,c.RecognizedFlows,Age(c.LastGameDataMs),c.TcpGaps,
            c.PendingOwnershipSegments,c.DiscardedBeforeOwnership,c.Errors,c.State,c.Issue);
        var selected=new DiagnosticCollection(collection.HasStoredValues,collection.Trusted,collection.IdentityConfirmed,collection.PendingSnapshots,
            Age(collection.LastSnapshotMs),Age(collection.LastGainMs),collection.AcceptedGainMessages,collection.AcceptedSoulAmount,
            Age(collection.LastLossMs),collection.LastLoss,collection.Route);
        var recent=events.Where(e=>e.Time<=now && now-e.Time<=TimeSpan.FromMinutes(30)).TakeLast(20).Select(e=>
            new DiagnosticEvent((long)(now-e.Time).TotalSeconds,e.Kind switch
            {
                "capture"=>DiagnosticEventKind.CaptureChanged,"session"=>DiagnosticEventKind.SessionChanged,
                "snapshot"=>DiagnosticEventKind.CollectionSynchronized,"stream-loss"=>DiagnosticEventKind.ContinuityLost,
                "invalid-pet-message" or "ignored-pet-message"=>DiagnosticEventKind.MessageRejected,_=>DiagnosticEventKind.Other
            })).ToArray();
        return new(1,version.ToString(),now.ToUniversalTime(),mode,npcap,safe,selected,decoded,rejected,recent);
    }
}
