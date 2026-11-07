using System.Text;
using AionMeter.Core.Capture;
using AionMeter.Core.Events;
using AionMeter.Core.Game;
using AionMeter.Core.Protocol;
using K4os.Compression.LZ4;
using SoulTracker.Core;
using static SoulTracker.Tests.SessionIntegrityTests;
namespace SoulTracker.Tests;

public sealed class TransportIntegrityTests
{
    private static byte[] Identity()
    {
        using var ms=new MemoryStream();using var w=new BinaryWriter(ms);
        w.Write((byte)42);w.Write(0);w.Write((byte)1);w.Write((byte)4);w.Write(Encoding.UTF8.GetBytes("Demo"));
        w.Write((ushort)1101);w.Write(13);return Frame(0x3336,ms.ToArray());
    }
    [Theory][InlineData(1)][InlineData(7)][InlineData(42)][InlineData(73)][InlineData(111)][InlineData(2026)]
    public void GoldenCollectionSurvivesFragmentationDuplicatesOverlapsReorderAndSequenceWrap(int seed)
    {
        for(var trial=0;trial<30;trial++)
        {
            var random=new Random(seed*1000+trial);
            var amounts=Enumerable.Range(0,50).Select(_=>random.Next(1,4)).ToArray();
            var bytes=new List<byte>{6,0,54};bytes.AddRange(Identity());bytes.AddRange(Frame(PetProtocol.SnapshotOpcode,Snapshot()));
            foreach(var n in amounts)bytes.AddRange(Frame(PetProtocol.GainOpcode,Gain(n)));
            var data=bytes.ToArray();var received=0;
            var state=new CollectionState([new(1120,"Pet","Fera","")]);
            var session=new CollectionSession(state,(g,_)=>received+=g.Count,(_,_)=>{},()=>{});
            PacketPipeline pipeline=null!;
            pipeline=new(GameData.Empty,e=>{if(e is SelfIdentifiedEvent id)session.Identify(pipeline.CurrentContext!,id.TimeMs,id.Name,id.ServerId);}){LockThreshold=1};
            pipeline.Parser.Tap=(t,op,b)=>session.Process(pipeline.CurrentContext!,t,op,b);
            pipeline.LossObserved+=session.ObserveLoss;
            var segments=new List<(int Offset,int Count)>();
            for(var at=0;at<data.Length;){var n=Math.Min(random.Next(1,32),data.Length-at);segments.Add((at,n));at+=n;}
            segments.AddRange(segments.Take(10)); // exact duplicate capture
            for(var i=0;i<10;i++){var at=random.Next(data.Length);segments.Add((at,Math.Min(random.Next(1,60),data.Length-at)));}
            segments=segments.OrderBy(_=>random.Next()).ToList();
            var start=uint.MaxValue-40;
            pipeline.OnSegment(100,Flow,unchecked(start-1),true,false,[],42);
            for(var i=0;i<segments.Count;i++)
            {var s=segments[i];pipeline.OnSegment(101+i,Flow,unchecked(start+(uint)s.Offset),false,false,data.AsSpan(s.Offset,s.Count),42);}
            // Independent oracle: total soul cost is 5 + 25 + 75. Starting at level 2 / 10 = 40.
            var total=40+amounts.Sum();var pet=Assert.Single(state.View().Pets);
            Assert.True(state.IsTrusted,$"seed={seed}; trial={trial}");Assert.Equal(0,pipeline.Gaps);
            Assert.Equal(Math.Min(105,total),pet.EquivalentTotal);Assert.Equal(total>=105?3:2,pet.Level);
            Assert.Equal(total-(total>=105?105:30),pet.Souls);Assert.Equal(amounts.Length,received);
        }
    }
    [Fact] public void HandlerExceptionReportsItsStreamAndDoesNotEscapeCaptureLoop()
    {
        var p=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};var losses=new List<StreamLoss>();
        p.LossObserved+=losses.Add;p.Parser.Tap=(_,op,_)=>{if(op==PetProtocol.GainOpcode)throw new InvalidOperationException("synthetic handler failure");};
        p.OnSegment(0,Flow,9,true,false,[],42);
        p.OnSegment(1,Flow,10,false,false,[6,0,54,..Frame(PetProtocol.GainOpcode,Gain())],42);
        var loss=Assert.Single(losses);Assert.Equal(42,loss.Stream.ProcessId);Assert.Contains("Packet handler failed",loss.Evidence.Reason);
        Assert.Null(p.CurrentContext);Assert.Null(p.CurrentFlow);
    }
    [Fact] public void CorruptBundleSizeIsRejectedBeforeAnyInnerPacket()
    {
        var inner=Frame(PetProtocol.GainOpcode,Gain());var compressed=new byte[LZ4Codec.MaximumOutputSize(inner.Length)];
        var n=LZ4Codec.Encode(inner,compressed);var body=new List<byte>{255,255};
        body.AddRange(BitConverter.GetBytes(inner.Length+1));body.AddRange(compressed.Take(n));
        var messages=0;var losses=0;var decoder=new FrameDecoder((_,_)=>messages++);decoder.DataLost+=_=>losses++;
        decoder.Feed([(byte)(body.Count+4),..body]);Assert.Equal(0,messages);Assert.Equal(1,losses);
    }
    [Fact] public void PrelockOverflowReportsLossOnlyAfterGameRecognition()
    {
        var p=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};var losses=new List<StreamLoss>();p.LossObserved+=losses.Add;
        p.OnSegment(0,Flow,9,true,false,[],42);uint seq=10;
        for(var i=0;i<10;i++){p.OnSegment(i+1,Flow,seq,false,false,new byte[65536],42);seq+=65536;}
        Assert.Empty(losses);p.OnSegment(20,Flow,seq,false,false,[6,0,54],42);
        Assert.Contains(losses,x=>x.Evidence.Reason=="Pre-lock buffer full");
    }
    [Fact] public void UnknownTrafficLossIsReportedOnlyAfterOwnershipConfirmation()
    {
        var delivered=0;var losses=new List<LossEvidence>();var gate=new GameConnectionGate(_=>delivered++);gate.DataLost+=losses.Add;
        gate.Push(new(100,Flow,1,false,false,[1,2,3]));gate.Update([],10200);
        Assert.Empty(losses);Assert.Equal(0,delivered);
        gate.Update([new(Flow.Dst,Flow.DstPort,Flow.Src,Flow.SrcPort,ProcessId:42)],11000);
        var loss=Assert.Single(losses);Assert.Equal(42,loss.ProcessId);Assert.Equal(1u,loss.Sequence);Assert.Equal(4u,loss.EndSequence);
    }
    [Fact] public void OwnershipBufferOverflowSurvivesDelayedPidLookup()
    {
        var losses=new List<LossEvidence>();var gate=new GameConnectionGate(_=>{});gate.DataLost+=losses.Add;
        for(var i=0;i<4097;i++)gate.Push(new(i,Flow,(uint)i,false,false,[1]));
        Assert.Empty(losses);gate.Update([new(Flow.Dst,Flow.DstPort,Flow.Src,Flow.SrcPort,ProcessId:42)],5000);
        Assert.Contains(losses,x=>x.Reason=="Ownership buffer full" && x.ProcessId==42);
    }
    [Fact] public void UnownedLossJournalIsBounded()
    {
        var gate=new GameConnectionGate(_=>{});
        for(var i=0;i<1000;i++)gate.Push(new(100,Flow with {DstPort=(ushort)(50000+i)},1,false,false,[1]));
        gate.Update([],12000);Assert.InRange(gate.PendingLossCount,1,512);
    }
    [Fact] public void SameTupleChangingProcessStartsNewGenerationEvenWithoutSyn()
    {
        var contexts=new List<StreamContext>();var p=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};
        p.Parser.Tap=(_,_,_)=>contexts.Add(p.CurrentContext!);
        p.OnSegment(10,Flow,100,false,false,[6,0,54],42);p.OnSegment(20,Flow,100,false,false,[6,0,54],99);
        Assert.Equal(2,contexts.Count);Assert.NotEqual(contexts[0].Generation,contexts[1].Generation);Assert.Equal(99,contexts[1].ProcessId);
    }
    [Fact] public void LongSilenceIsNotEvidenceOfDataLoss()
    {
        var losses=0;var count=0;var p=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};p.LossObserved+=_=>losses++;p.Parser.Tap=(_,_,_)=>count++;
        p.OnSegment(0,Flow,9,true,false,[]);p.OnSegment(1,Flow,10,false,false,[6,0,54]);
        p.OnSegment(3_600_000,Flow,13,false,false,[6,0,54]);Assert.Equal(0,losses);Assert.Equal(2,count);
    }
    [Fact] public void DroppedDuplicateAlreadyDeliveredDoesNotInvalidateStream()
    {
        var losses=0;var p=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};p.LossObserved+=_=>losses++;
        p.OnSegment(0,Flow,9,true,false,[],42);p.OnSegment(1,Flow,10,false,false,[6,0,54],42);
        p.ReportCaptureLoss(new(Flow,2,"Delayed duplicate discarded",10,13,42));Assert.Equal(0,losses);
    }
    [Fact] public void KnownDroppedRangeRecoveredByRetransmissionDoesNotInvalidateStream()
    {
        var losses=0;var count=0;var p=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};
        p.LossObserved+=_=>losses++;p.Parser.Tap=(_,_,_)=>count++;
        p.OnSegment(0,Flow,9,true,false,[],42);p.OnSegment(1,Flow,10,false,false,[6,0,54],42);
        p.ReportCaptureLoss(new(Flow,2,"Ownership buffer expired",13,16,42));
        p.OnSegment(3,Flow,16,false,false,[6,0,54],42);Assert.Equal(0,losses);Assert.Equal(1,count);
        p.OnSegment(100,Flow,13,false,false,[6,0,54],42);
        p.OnSegment(10000,Flow,19,false,false,[6,0,54],42);
        Assert.Equal(0,losses);Assert.Equal(4,count);
    }
    [Fact] public void KnownDroppedRangeWithoutRecoveryReportsItsBoundaries()
    {
        var losses=new List<StreamLoss>();var p=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};p.LossObserved+=losses.Add;
        p.OnSegment(0,Flow,9,true,false,[],42);p.OnSegment(1,Flow,10,false,false,[6,0,54],42);
        p.ReportCaptureLoss(new(Flow,2,"Ownership buffer expired",13,16,42));
        p.OnSegment(3,Flow,16,false,false,[6,0,54],42);
        p.OnSegment(6000,Flow,19,false,false,[6,0,54],42);
        Assert.Contains(losses,x=>x.Evidence.Sequence==13 && x.Evidence.EndSequence==16 && x.Stream.ProcessId==42);
    }
}
