using AionMeter.Core.Protocol;
using AionMeter.Core.Capture;
using AionMeter.Core.Game;
using AionMeter.Core.Events;
using K4os.Compression.LZ4;
using SoulTracker.Core;
namespace SoulTracker.Tests;

public sealed class BundleIntegrityTests
{
    private static byte[] Frame(byte[] payload)
    {
        var prefix=new List<byte>();var length=(uint)payload.Length+4;
        do {var next=(byte)(length&127);length>>=7;prefix.Add((byte)(next|(length>0?128:0)));}while(length>0);
        return [..prefix,..payload];
    }
    private static byte[] Packet(ushort opcode,params byte[] body)=>Frame([(byte)(opcode>>8),(byte)opcode,..body]);
    private static byte[] Bundle(byte[] raw,int? declaredSize=null)
    {
        var output=new byte[LZ4Codec.MaximumOutputSize(raw.Length)];var count=LZ4Codec.Encode(raw,output);
        return Frame([255,255,..BitConverter.GetBytes(declaredSize??raw.Length),..output.AsSpan(0,count)]);
    }
    [Fact] public void TruncatedBundleTailDoesNotEmitItsValidPrefix()
    {
        var packets=0;var losses=0;var decoder=new FrameDecoder((_,_)=>packets++);decoder.DataLost+=_=>losses++;
        decoder.Feed(Bundle([..Packet(0x0D90,1,2,3),9,0x0D]));
        Assert.Equal(0,packets);Assert.Equal(1,losses);
    }
    [Fact] public void InvalidNestedBundleDoesNotEmitAnySibling()
    {
        var packets=0;var losses=0;var decoder=new FrameDecoder((_,_)=>packets++);decoder.DataLost+=_=>losses++;
        var invalid=Bundle(Packet(0x0036),99);
        decoder.Feed(Bundle([..Packet(0x0D90,1),..invalid,..Packet(0x3336,2),..Packet(0x0090,3)]));
        Assert.Equal(0,packets);Assert.Equal(1,losses);
    }
    [Fact] public void FlagWithoutCompleteOpcodeReportsLoss()
    {
        var packets=0;var losses=0;var decoder=new FrameDecoder((_,_)=>packets++);decoder.DataLost+=_=>losses++;
        decoder.Feed(Frame([0xF0,0x0D]));
        Assert.Equal(0,packets);Assert.Equal(1,losses);
    }
    [Fact] public void IncompleteNestedOpcodeRejectsWholeBundle()
    {
        var packets=0;var losses=0;var decoder=new FrameDecoder((_,_)=>packets++);decoder.DataLost+=_=>losses++;
        decoder.Feed(Bundle([..Packet(0x1234,1),..Frame([0xF0,0x0D]),..Packet(0x1234,2)]));
        Assert.Equal(0,packets);Assert.Equal(1,losses);
    }
    [Fact] public void ValidNestedBundlePreservesOrderBodiesFlagsAndFragmentation()
    {
        var random=new Random(83);var body=new byte[200];random.NextBytes(body);
        var packets=new List<(ushort Opcode,byte[] Body)>();var losses=0;
        var decoder=new FrameDecoder((op,b)=>packets.Add((op,b.ToArray())));decoder.DataLost+=_=>losses++;
        var inner=Bundle([..Packet(0x1235,2),..Bundle([..Frame([0xF0,0x12,0x36,..body]),0,0])]);
        var bytes=Bundle([..Packet(0x1234,1),0,..inner,..Packet(0x1237,4)]);
        for(var at=0;at<bytes.Length;){var count=Math.Min(random.Next(1,13),bytes.Length-at);decoder.Feed(bytes.AsSpan(at,count));at+=count;}
        Assert.Equal(0,losses);Assert.Equal(new ushort[]{0x1234,0x1235,0x1236,0x1237},packets.Select(x=>x.Opcode));
        Assert.Equal(new byte[]{1},packets[0].Body);Assert.Equal(new byte[]{2},packets[1].Body);
        Assert.Equal(body,packets[2].Body);Assert.Equal(new byte[]{4},packets[3].Body);
    }
    [Theory][InlineData("depth")][InlineData("nodes")][InlineData("frames")][InlineData("expanded")]
    public void AggregateBundleLimitsRejectBeforeDispatch(string limit)
    {
        byte[] raw;
        if(limit=="depth")
        {raw=Packet(0x1234,1);for(var i=0;i<5;i++)raw=Bundle(raw);}
        else if(limit=="nodes")
        {var child=Bundle([0]);raw=Enumerable.Range(0,FrameDecoder.MaxBundleNodes).SelectMany(_=>child).ToArray();}
        else if(limit=="frames")
        {var child=Packet(0x0036);raw=Enumerable.Range(0,FrameDecoder.MaxBundleFrames+1).SelectMany(_=>child).ToArray();}
        else
        {var child=Bundle(new byte[FrameDecoder.MaxBundle]);raw=Enumerable.Range(0,5).SelectMany(_=>child).ToArray();}
        var packets=0;var reasons=new List<string>();var decoder=new FrameDecoder((_,_)=>packets++);decoder.DataLost+=reasons.Add;
        decoder.Feed(Bundle([..Packet(0x1234,42),..raw]));
        Assert.Equal(0,packets);Assert.Contains("limit",Assert.Single(reasons));
    }
    [Theory][InlineData(0x10)][InlineData(0x1F)][InlineData(0x70)]
    public void OverflowingUintVarintIsRejectedWithoutAdvancing(int lastByte)
    {
        var offset=0;
        Assert.False(Wire.TryVarint([0x86,0x80,0x80,0x80,(byte)lastByte],ref offset,out var value));
        Assert.Equal(0,offset);Assert.Equal(0u,value);
    }
    [Fact] public void MaximumUintVarintRemainsValid()
    {
        var offset=0;Assert.True(Wire.TryVarint([255,255,255,255,15],ref offset,out var value));
        Assert.Equal(uint.MaxValue,value);Assert.Equal(5,offset);
    }
    [Fact] public void CorruptBundleCannotMutateOrResynchronizeCollectionAndNextCleanBundleCan()
    {
        var state=new CollectionState([new(1120,"Pet","Fera","")]);var gains=0;
        var session=new CollectionSession(state,(g,_)=>gains+=g.Count,(_,_)=>{},()=>{});
        PacketPipeline pipeline=null!;
        pipeline=new(GameData.Empty,e=>{if(e is SelfIdentifiedEvent id)session.Identify(pipeline.CurrentContext!,id.TimeMs,id.Name,id.ServerId);}){LockThreshold=1};
        pipeline.Parser.Tap=(t,op,body)=>session.Process(pipeline.CurrentContext!,t,op,body);
        pipeline.LossObserved+=session.ObserveLoss;
        var flow=SessionIntegrityTests.Flow;uint sequence=100;
        void Send(byte[] data,long time){pipeline.OnSegment(time,flow,sequence,false,false,data,42);sequence+=(uint)data.Length;}
        pipeline.OnSegment(0,flow,sequence-1,true,false,[],42);
        Send([..Packet(0x0036),..TransportIntegrityTests.Identity(),..Packet(PetProtocol.SnapshotOpcode,SessionIntegrityTests.Snapshot())],1);
        Assert.True(state.IsTrusted);Assert.Equal(10,Assert.Single(state.View().Pets).Souls);
        Send(Bundle([..Packet(PetProtocol.GainOpcode,SessionIntegrityTests.Gain()),..Bundle(Packet(0x0036),99),
            ..TransportIntegrityTests.Identity(),..Packet(PetProtocol.SnapshotOpcode,SessionIntegrityTests.Snapshot(55)),
            ..Packet(PetProtocol.GainOpcode,SessionIntegrityTests.Gain())]),2);
        Assert.False(state.IsTrusted);Assert.Equal(10,Assert.Single(state.View().Pets).Souls);Assert.Equal(0,gains);
        Send(Bundle([..TransportIntegrityTests.Identity(),..Packet(PetProtocol.SnapshotOpcode,SessionIntegrityTests.Snapshot(12)),
            ..Packet(PetProtocol.GainOpcode,SessionIntegrityTests.Gain())]),3);
        Assert.True(state.IsTrusted);Assert.Equal(13,Assert.Single(state.View().Pets).Souls);Assert.Equal(1,gains);
    }
}
