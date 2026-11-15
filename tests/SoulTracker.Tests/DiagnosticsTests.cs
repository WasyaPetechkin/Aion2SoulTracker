using System.Text.Json;
using AionMeter.Core.Capture;
using AionMeter.Core.Game;
using SoulTracker.Core;
namespace SoulTracker.Tests;

public sealed class DiagnosticsTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.FromUnixTimeMilliseconds(2_000_000);
    private static CaptureDiagnostics Capture()=>new(1,1,0,1,0,1_999_000,150,140,1,1_999_500,0,0,0,0,CaptureState.Capturing,CaptureIssue.None);
    private static CollectionDiagnostics Collection()=>new(true,true,true,0,1_990_000,0,0,0,0,StreamIssue.None,DiagnosticRoute.Network);
    private static DiagnosticReport Report(CaptureDiagnostics? capture=null,CollectionDiagnostics? collection=null,DiagnosticMode mode=DiagnosticMode.Live,bool npcap=true)=>
        DiagnosticReport.Create(new Version(0,3,9),Now,mode,npcap,capture??Capture(),collection??Collection(),42,0,[]);

    [Fact] public void SynchronizedSessionWithoutGainsIsReady()
    {var report=Report();Assert.Equal(DiagnosticOutcome.Ready,report.Outcome);Assert.Null(report.Collection.GainAgeSeconds);}
    [Fact] public void QuietNetworkDoesNotBecomeResyncOrLoss()
    {var report=Report(Capture() with{LastGameDataMs=1_980_000});Assert.Equal(DiagnosticOutcome.WaitingForTraffic,report.Outcome);Assert.True(report.Collection.Trusted);}
    [Fact] public void UnrelatedCaptureLossTotalsDoNotInvalidateHealthySession()
    {Assert.Equal(DiagnosticOutcome.Ready,Report(Capture() with{TcpGaps=5,DiscardedBeforeOwnership=200,Errors=1,State=CaptureState.Error,Issue=CaptureIssue.AdapterOpenFailed}).Outcome);}
    [Fact] public void SavedCollectionIsNotALiveSnapshot()
    {Assert.Equal(DiagnosticOutcome.AwaitingCollection,Report(collection:Collection() with{Trusted=false,LastSnapshotMs=0}).Outcome);}
    [Fact] public void PendingSnapshotWaitsForIdentity()
    {Assert.Equal(DiagnosticOutcome.AwaitingIdentity,Report(collection:Collection() with{Trusted=false,IdentityConfirmed=false,LastSnapshotMs=0,PendingSnapshots=1}).Outcome);}
    [Fact] public void ActualSelectedStreamLossRequiresSync()
    {Assert.Equal(DiagnosticOutcome.NeedsResync,Report(collection:Collection() with{Trusted=false,LastLossMs=1_999_000,LastLoss=StreamIssue.TcpGap}).Outcome);}
    [Theory][InlineData(DiagnosticMode.Offline,DiagnosticOutcome.Offline)][InlineData(DiagnosticMode.Replay,DiagnosticOutcome.Replay)]
    public void NonLiveModesNeverClaimLiveSuccess(DiagnosticMode mode,DiagnosticOutcome outcome)=>Assert.Equal(outcome,Report(mode:mode).Outcome);
    [Fact] public void MissingDriverPrecedesOtherLiveStages()=>Assert.Equal(DiagnosticOutcome.NpcapMissing,Report(npcap:false).Outcome);
    [Fact] public void DriverLoadFailureIsReportedEvenWhenLibraryFileExists()=>Assert.Equal(DiagnosticOutcome.NpcapMissing,Report(Capture() with{Issue=CaptureIssue.NpcapUnavailable}).Outcome);
    [Fact] public void ProcessLookupFailureIsNotPresentedAsGameMissing()=>Assert.Equal(DiagnosticOutcome.CaptureUnavailable,Report(Capture() with{DiscoveryTimeMs=0,Processes=0,NetworkReaders=0,State=CaptureState.Error,Issue=CaptureIssue.MonitorFailure}).Outcome);
    [Theory][InlineData(0,0,0,0,DiagnosticOutcome.GameNotFound)][InlineData(1,0,0,0,DiagnosticOutcome.ConnectionNotFound)]
    [InlineData(1,1,0,0,DiagnosticOutcome.CaptureUnavailable)][InlineData(1,1,1,0,DiagnosticOutcome.ProtocolNotRecognized)]
    public void IdentifiesTheFirstMissingStage(int processes,int connections,int readers,int flows,DiagnosticOutcome expected)=>
        Assert.Equal(expected,Report(Capture() with{Processes=processes,Connections=connections,NetworkReaders=readers,RecognizedFlows=flows}).Outcome);
    [Fact] public void PreparingReportCannotLeakRawStateOrErrors()
    {
        const string secret="SecretCharacter_77 203.0.113.91 fe80::abcd C:\\Users\\PrivateOwner\\secret VPN_PRIVATE_ADAPTER";
        using var session=new TrackerSession([new(1120,"Pet","Fera",secret)],GameData.Empty);
        session.State.Identify(1_000,secret,1101);session.State.Process(2_000,PetProtocol.SnapshotOpcode,SessionIntegrityTests.Snapshot());
        session.ObserveCaptureStatus(new(CaptureState.Error,secret));
        var json=session.Diagnostics(new Version(0,3,9),true,DateTimeOffset.UtcNow.AddSeconds(1)).ToJson();
        foreach(var value in new[]{"SecretCharacter_77","203.0.113.91","fe80","PrivateOwner","VPN_PRIVATE_ADAPTER","ServerId","ProcessId","Flow","History","Source"})
            Assert.DoesNotContain('"'+value+'"',json);
        Assert.DoesNotContain("SecretCharacter",json);Assert.DoesNotContain("203.0.113",json);Assert.DoesNotContain("PrivateOwner",json);
        Assert.DoesNotContain("VPN_PRIVATE",json);Assert.DoesNotContain("fe80",json);using var parsed=JsonDocument.Parse(json);
        Assert.Equal(1,parsed.RootElement.GetProperty("SchemaVersion").GetInt32());
    }
    [Fact] public void RecentEventsAreBoundedAndFreeTextNeverEntersExport()
    {
        var events=Enumerable.Range(0,200).Select(i=>new TrackerSession.NetworkEvent(Now.AddSeconds(-i),"private-kind",new string('X',10000))).ToArray();
        var report=DiagnosticReport.Create(new Version(0,3,9),Now,DiagnosticMode.Live,true,Capture(),Collection(),1,0,
            [new(Now.AddHours(-1),"snapshot","old"),..events,new(Now.AddMinutes(1),"snapshot","future")]);
        Assert.Equal(20,report.RecentEvents.Count);Assert.All(report.RecentEvents,e=>Assert.Equal(DiagnosticEventKind.Other,e.Kind));
        var json=report.ToJson();Assert.DoesNotContain("private-kind",json);Assert.DoesNotContain("XXXXX",json);Assert.True(json.Length<10000);
    }
    [Fact] public void TelemetryTracksAcceptedGainsWithoutDrainingHudQueueAndResetsForCharacter()
    {
        using var session=new TrackerSession([new(1120,"Pet","Fera","")],GameData.Empty);
        var context=new StreamContext(SessionIntegrityTests.Flow,1,42,1_000);
        session.Streams.Identify(context,1_000,"First",1101);
        session.Streams.Process(context,2_000,PetProtocol.SnapshotOpcode,SessionIntegrityTests.Snapshot());
        session.Streams.Process(context,3_000,PetProtocol.GainOpcode,SessionIntegrityTests.Gain(2));
        var report=session.Diagnostics(new Version(0,3,9),true,Now);
        Assert.Equal(1,report.Collection.AcceptedGainMessages);Assert.Equal(2,report.Collection.AcceptedSoulAmount);
        Assert.NotNull(report.Collection.GainAgeSeconds);Assert.Single(session.DrainReceivedSouls());
        session.Streams.Identify(context,4_000,"Second",1102);
        var next=session.Diagnostics(new Version(0,3,9),true,Now).Collection;
        Assert.Equal(0,next.AcceptedSoulAmount);Assert.Null(next.GainAgeSeconds);Assert.Null(next.SnapshotAgeSeconds);Assert.False(next.Trusted);
    }
    [Fact] public void RejectedGainDoesNotBecomeLastAcceptedGainAndUsesFailureTimestamp()
    {
        var state=new CollectionState([new(1120,"Pet","Fera","")]);var session=new CollectionSession(state,(_,_)=>{},(_,_)=>{},()=>{});
        var context=new StreamContext(SessionIntegrityTests.Flow,1,42,1000);
        session.Identify(context,1000,"First",1101);session.Process(context,2000,PetProtocol.SnapshotOpcode,SessionIntegrityTests.Snapshot());
        session.Process(context,5000,PetProtocol.GainOpcode,[1]);var diagnostic=session.Diagnostics();
        Assert.Equal(0,diagnostic.LastGainMs);Assert.Equal(5000,diagnostic.LastLossMs);Assert.Equal(StreamIssue.InvalidGain,diagnostic.LastLoss);
    }
    [Fact] public void MissingNpcapInNormalStartupIsNotMistakenForOfflineMode()
    {using var session=new TrackerSession([],GameData.Empty);Assert.Equal(DiagnosticOutcome.NpcapMissing,session.Diagnostics(new Version(0,3,9),false,liveRequested:true).Outcome);}
    [Fact] public void LossReasonCannotExportFreeFormSensitiveData()
    {
        var state=new CollectionState([]);var session=new CollectionSession(state,(_,_)=>{},(_,_)=>{},()=>{});
        var context=new StreamContext(SessionIntegrityTests.Flow,1,42,1000);session.Identify(context,1000,"First",1101);
        session.ObserveLoss(new(context,new(context.Flow,2000,"PrivateOwner 203.0.113.91",ProcessId:42)));
        var json=DiagnosticReport.Create(new Version(0,3,9),Now,DiagnosticMode.Live,true,Capture(),session.Diagnostics(),0,0,[]).ToJson();
        Assert.DoesNotContain("PrivateOwner",json);Assert.DoesNotContain("203.0.113.91",json);Assert.Contains("Other",json);
    }
}
