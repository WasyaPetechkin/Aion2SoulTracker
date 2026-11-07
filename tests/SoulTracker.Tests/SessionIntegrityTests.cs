using System.Net;
using AionMeter.Core.Capture;
using SoulTracker.Core;
namespace SoulTracker.Tests;

public sealed class SessionIntegrityTests
{
    internal static readonly FlowKey Flow=new(IPAddress.Parse("10.1.0.1"),13328,IPAddress.Parse("10.0.0.2"),50000);
    internal static byte[] Snapshot(int souls=10,int pet=1120,bool unlocked=true)
    {
        using var ms=new MemoryStream();using var w=new BinaryWriter(ms);
        w.Write(0);w.Write((byte)(unlocked?1:0));if(unlocked){w.Write(pet);w.Write(pet);w.Write(2);}
        w.Write((byte)1);w.Write(pet);w.Write(souls);return ms.ToArray();
    }
    internal static byte[] Gain(int amount=1,int pet=1120)
    {using var ms=new MemoryStream();using var w=new BinaryWriter(ms);w.Write((byte)1);w.Write(pet);w.Write(amount);return ms.ToArray();}
    private static byte[] OversizedGain()
    {
        using var ms=new MemoryStream();using var w=new BinaryWriter(ms);w.Write((byte)0x80);w.Write((byte)0x20);
        for(var i=0;i<4096;i++){w.Write(1120);w.Write(1_000_000);}return ms.ToArray();
    }
    internal static byte[] Frame(ushort op,byte[] body)=>[(byte)(body.Length+6),(byte)(op>>8),(byte)op,..body];
    private sealed class Setup
    {
        public CollectionState State=new([new(1120,"First pet","Fera",""),new(1106,"Second pet","Fera","")]);
        public List<SoulGain> Gains=[];
        public CollectionSession Session;
        public StreamContext A=new(Flow,1,42,1);
        public StreamContext B=new(Flow with {DstPort=50001},2,42,200);
        public Setup(){Session=new(State,(g,_)=>Gains.AddRange(g),(_,_)=>{},()=>{});}
        public void Confirm(){Session.Identify(A,10,"First",1101);Session.Process(A,20,PetProtocol.SnapshotOpcode,Snapshot());}
        public PetProgress Pet=>State.View().Pets.Single(p=>p.Id==1120);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void NewCharacterRequiresMatchingIdentityAndSnapshotInEitherOrder(bool snapshotFirst)
    {
        var f=new Setup();f.Confirm();
        if(snapshotFirst){f.Session.Process(f.B,210,PetProtocol.SnapshotOpcode,Snapshot(22,1106));f.Session.Identify(f.B,220,"Second",1102);}
        else {f.Session.Identify(f.B,210,"Second",1102);f.Session.Process(f.B,220,PetProtocol.SnapshotOpcode,Snapshot(22,1106));}
        Assert.True(f.State.IsTrusted);Assert.Equal("Second",f.State.View().Character);
        Assert.Equal(0,f.Pet.Souls);Assert.Equal(22,f.State.View().Pets.Single(p=>p.Id==1106).Souls);
        Assert.False(f.Session.Process(f.A,230,PetProtocol.GainOpcode,Gain()));
        Assert.False(f.Session.Process(f.A,240,PetProtocol.SnapshotOpcode,Snapshot(70)));
        f.Session.Identify(f.A,250,"First",1101);Assert.Equal("Second",f.State.View().Character);
    }
    [Fact] public void SnapshotWaitsForIdentityAndReplaysOnlySubsequentGains()
    {
        var f=new Setup();f.Session.Process(f.A,10,PetProtocol.SnapshotOpcode,Snapshot());
        f.Session.Process(f.A,11,PetProtocol.GainOpcode,Gain());Assert.False(f.State.IsTrusted);Assert.Empty(f.Gains);
        f.Session.Identify(f.A,12,"First",1101);Assert.True(f.State.IsTrusted);Assert.Equal(11,f.Pet.Souls);Assert.Single(f.Gains);
    }
    [Fact] public void SecondProcessCannotReplaceOrInvalidateSelectedProcess()
    {
        var f=new Setup();f.Confirm();var other=f.B with {ProcessId=99};
        f.Session.Identify(other,210,"Other",1102);f.Session.Process(other,220,PetProtocol.SnapshotOpcode,Snapshot(70));
        f.Session.ObserveLoss(new(other,new(other.Flow,230,"gap")));
        Assert.True(f.State.IsTrusted);Assert.Equal("First",f.State.View().Character);Assert.Equal(10,f.Pet.Souls);
    }
    [Fact] public void NewVpnFlowWithoutSnapshotCannotInheritTrust()
    {
        var f=new Setup();f.Confirm();
        Assert.False(f.Session.Process(f.B,210,PetProtocol.GainOpcode,Gain()));
        Assert.False(f.State.IsTrusted);Assert.Equal(10,f.Pet.Souls);
        f.Session.Identify(f.B,220,"First",1101);f.Session.Process(f.B,230,PetProtocol.SnapshotOpcode,Snapshot(11));
        Assert.True(f.State.IsTrusted);Assert.Equal(11,f.Pet.Souls);
    }
    [Fact] public void SameCharacterTransferDoesNotCountMirroredGainsTwice()
    {
        var f=new Setup();f.Confirm();f.Session.Process(f.A,30,PetProtocol.GainOpcode,Gain());
        f.Session.Identify(f.B,210,"First",1101);f.Session.Process(f.B,220,PetProtocol.SnapshotOpcode,Snapshot(11));
        f.Session.Process(f.A,230,PetProtocol.GainOpcode,Gain());f.Session.Process(f.B,240,PetProtocol.GainOpcode,Gain());
        Assert.Equal(12,f.Pet.Souls);Assert.Equal(2,f.Gains.Count);
    }
    [Fact] public void SameTupleNewGenerationRejectsLateOldMessages()
    {
        var f=new Setup();f.Confirm();var reused=f.B with {Flow=Flow};
        f.Session.Process(reused,210,PetProtocol.SnapshotOpcode,Snapshot(20));f.Session.Identify(reused,220,"First",1101);
        f.Session.Process(f.A,230,PetProtocol.GainOpcode,Gain());Assert.Equal(20,f.Pet.Souls);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void PreviouslyUnusedPreopenedConnectionCanTakeOverInEitherOrder(bool snapshotFirst)
    {
        var f=new Setup();var preopened=f.A;var active=f.B with {StartedMs=2};
        f.Session.Process(preopened,1,0x0036,[]);
        f.Session.Identify(active,10,"First",1101);f.Session.Process(active,20,PetProtocol.SnapshotOpcode,Snapshot());
        if(snapshotFirst){f.Session.Process(preopened,30,PetProtocol.SnapshotOpcode,Snapshot(22));f.Session.Identify(preopened,40,"First",1101);}
        else {f.Session.Identify(preopened,30,"First",1101);f.Session.Process(preopened,40,PetProtocol.SnapshotOpcode,Snapshot(22));}
        Assert.True(f.State.IsTrusted);Assert.Equal(preopened,f.Session.ActiveStream);Assert.Equal(22,f.Pet.Souls);
        Assert.False(f.Session.Process(active,50,PetProtocol.GainOpcode,Gain()));
        f.Session.Identify(active,60,"First",1101);Assert.Equal(preopened,f.Session.ActiveStream);
    }
    [Fact] public void LossAffectsOnlyItsGenerationAndNeedsFreshIdentityAndSnapshot()
    {
        var f=new Setup();f.Confirm();f.Session.ObserveLoss(new(f.B,new(f.B.Flow,210,"unrelated gap")));
        Assert.True(f.State.IsTrusted);
        f.Session.ObserveLoss(new(f.A,new(Flow,30,"gap")));Assert.False(f.State.IsTrusted);
        Assert.False(f.Session.Process(f.A,31,PetProtocol.GainOpcode,Gain()));
        f.Session.Process(f.A,40,PetProtocol.SnapshotOpcode,Snapshot(15));Assert.False(f.State.IsTrusted);
        f.Session.Identify(f.A,50,"First",1101);Assert.True(f.State.IsTrusted);Assert.Equal(15,f.Pet.Souls);
    }
    [Fact] public void OldSnapshotCannotRollBackNewerGain()
    {
        var f=new Setup();f.Confirm();f.Session.Process(f.A,100,PetProtocol.GainOpcode,Gain());
        Assert.False(f.Session.Process(f.A,90,PetProtocol.SnapshotOpcode,Snapshot(3)));
        Assert.False(f.State.Process(90,PetProtocol.SnapshotOpcode,Snapshot(3)));Assert.Equal(11,f.Pet.Souls);
    }
    [Fact] public void SameStreamCharacterChangeDoesNotUseThirtySecondGuess()
    {
        var f=new Setup();f.Confirm();f.Session.Identify(f.A,25,"First",1101); // repeated old identity
        f.Session.Process(f.A,30,PetProtocol.SnapshotOpcode,Snapshot(22,1106));
        Assert.False(f.State.IsTrusted);Assert.Equal(10,f.Pet.Souls);
        f.Session.Identify(f.A,40,"Second",1102);
        Assert.True(f.State.IsTrusted);Assert.Equal(0,f.Pet.Souls);Assert.Equal("Second",f.State.View().Character);
    }
    [Fact] public void DistinctIdenticalGainsAreBothCounted()
    {
        var f=new Setup();f.Confirm();f.Session.Process(f.A,30,PetProtocol.GainOpcode,Gain());f.Session.Process(f.A,30,PetProtocol.GainOpcode,Gain());
        Assert.Equal(12,f.Pet.Souls);Assert.Equal(2,f.Gains.Count);
    }
    [Fact] public void ProcessExitRetiresAllOldContexts()
    {
        var f=new Setup();f.Confirm();f.Session.ObserveProcesses([99]);Assert.False(f.State.IsTrusted);
        Assert.False(f.Session.Process(f.A,300,PetProtocol.SnapshotOpcode,Snapshot()));
        f.Session.Identify(f.B with {ProcessId=99},310,"Other",1101);
        f.Session.Process(f.B with {ProcessId=99},320,PetProtocol.SnapshotOpcode,Snapshot(25));Assert.Equal(25,f.Pet.Souls);
    }
    [Fact] public void AlreadyOpenSecondProcessCanSynchronizeAfterSelectedProcessExits()
    {
        var f=new Setup();f.Confirm();var other=f.B with {ProcessId=99};
        f.Session.Process(other,210,0x0036,[]);f.Session.ObserveProcesses([99]);
        f.Session.Process(other,220,PetProtocol.SnapshotOpcode,Snapshot(25));f.Session.Identify(other,230,"Other",1102);
        Assert.True(f.State.IsTrusted);Assert.Equal("Other",f.State.View().Character);Assert.Equal(25,f.Pet.Souls);
        Assert.False(f.Session.Process(f.A,240,PetProtocol.GainOpcode,Gain()));
    }
    [Fact] public void OlderGainIsNotSilentlyDroppedWhileDisplayingTrustedCounter()
    {
        var f=new Setup();f.Confirm();f.Session.Process(f.A,100,PetProtocol.GainOpcode,Gain());
        Assert.False(f.Session.Process(f.A,90,PetProtocol.GainOpcode,Gain()));Assert.False(f.State.IsTrusted);
    }
    [Fact] public void CandidateFloodIsBoundedAndDoesNotRetireActiveStream()
    {
        var f=new Setup();f.Confirm();
        for(var i=2;i<100;i++)f.Session.Process(new(Flow with {DstPort=(ushort)(50000+i)},i,42,i),200,0x0036,[]);
        Assert.InRange(f.Session.PendingStreams,1,32);
        Assert.True(f.Session.Process(f.A,300,PetProtocol.GainOpcode,Gain()));Assert.Equal(11,f.Pet.Souls);
    }
    [Fact] public void PendingGainOverflowCannotBecomeTrustedAfterIdentity()
    {
        var f=new Setup();f.Session.Process(f.A,10,PetProtocol.SnapshotOpcode,Snapshot());
        for(var i=0;i<4097;i++)f.Session.Process(f.A,11+i,PetProtocol.GainOpcode,Gain());
        f.Session.Identify(f.A,5000,"First",1101);Assert.False(f.State.IsTrusted);Assert.Empty(f.Gains);
    }
    [Fact] public void EmptyAndUnownedOnlySnapshotsReplaceWholeCollection()
    {
        var f=new Setup();f.Confirm();f.Session.Identify(f.B,210,"Second",1102);
        f.Session.Process(f.B,220,PetProtocol.SnapshotOpcode,[0,0,0,0,0,0]);Assert.True(f.State.IsTrusted);Assert.Equal(0,f.Pet.Souls);
        f.Session.Process(f.B,230,PetProtocol.SnapshotOpcode,Snapshot(2,1106,false));f.Session.Identify(f.B,240,"Second",1102);
        var p=f.State.View().Pets.Single(p=>p.Id==1106);Assert.Equal(0,p.Level);Assert.Equal(2,p.Souls);
    }
    [Fact] public void OversizedAggregateGainDoesNotPartiallyMutateCollection()
    {
        var state=new CollectionState([new(1120,"Pet","Fera","")]);state.Process(10,PetProtocol.SnapshotOpcode,Snapshot());
        Assert.False(state.Process(20,PetProtocol.GainOpcode,OversizedGain()));
        Assert.Equal(10,Assert.Single(state.View().Pets).Souls);Assert.Single(state.View().History);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void UnapplicableGainCannotLeaveTrustedSession(bool buffered)
    {
        var f=new Setup();
        if(!buffered)f.Session.Identify(f.A,10,"First",1101);
        f.Session.Process(f.A,20,PetProtocol.SnapshotOpcode,Snapshot());
        f.Session.Process(f.A,30,PetProtocol.GainOpcode,OversizedGain());
        if(buffered)f.Session.Identify(f.A,40,"First",1101);
        Assert.False(f.State.IsTrusted);Assert.Equal(10,f.Pet.Souls);Assert.Empty(f.Gains);
    }
}
