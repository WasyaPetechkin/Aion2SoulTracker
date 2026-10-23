using System.Net;
using AionMeter.Core.Capture;
using AionMeter.Core.Game;
using AionMeter.Core.Protocol;
namespace SoulTracker.Tests;
public sealed class NetworkTransitionTests
{
    private static readonly IPAddress Local=IPAddress.Parse("10.0.0.2"),Server=IPAddress.Parse("10.1.0.1");
    private static readonly FlowKey Flow=new(Server,13328,Local,50000);
    private static byte[] Heartbeat=>[14,0,54,0,0,0,0,0,0,0,0];
    private static byte[] Data=>Heartbeat.Concat(new byte[]{7,13,144,1}).ToArray();
    [Fact] public void TransferFilterDoesNotChangeWithServerOrClientPort()
    {
        CaptureAdapter[] adapters=[new("vpn",false,[Local])];
        var a=CapturePlan.Create([new(Local,50000,Server,13328)],adapters);
        var b=CapturePlan.Create([new(Local,50001,IPAddress.Parse("10.1.0.3"),13328)],adapters);
        Assert.Equal(a["vpn"],b["vpn"]);Assert.Equal("tcp and src port 13328",a["vpn"]);
    }
    [Fact] public void OverlappingVpnAndDirectConnectionsKeepBothAdapters()
    {
        var direct=IPAddress.Parse("192.168.1.2");
        var plan=CapturePlan.Create([new(Local,1,Server,13328),new(direct,2,Server,13328)],
            [new("vpn",false,[Local]),new("ethernet",false,[direct]),new("loopback",true,[IPAddress.Loopback])]);
        Assert.Equal(2,plan.Count);Assert.Contains("vpn",plan.Keys);Assert.Contains("ethernet",plan.Keys);
    }
    [Fact] public void InternalLoopbackDoesNotReplaceMissingGameConnection()
    {
        var plan=CapturePlan.Create([new(IPAddress.Loopback,100,IPAddress.Loopback,101)], [new("loopback",true,[IPAddress.Loopback])]);
        Assert.Empty(plan);
    }
    [Fact] public void Port13328LoopbackProxyIsSupported()
    {
        var plan=CapturePlan.Create([new(IPAddress.Loopback,100,IPAddress.Loopback,13328)], [new("loopback",true,[IPAddress.Loopback])]);
        Assert.Single(plan);
    }
    [Fact] public void AcceleratorOnDynamicLocalPortIsCaptured()
    {
        var plan=CapturePlan.Create([new(IPAddress.Loopback,59783,IPAddress.Loopback,56549,true)],
            [new("loopback",true,[IPAddress.Loopback]),new("ethernet",false,[Local])]);
        Assert.Single(plan);
        Assert.Equal("tcp and src host 127.0.0.1 and src port 56549",plan["loopback"]);
    }
    [Fact] public void AcceleratorAndDirectServerAreCapturedDuringTransition()
    {
        var plan=CapturePlan.Create([new(Local,50000,Server,13328),new(IPAddress.Loopback,59783,IPAddress.Loopback,56549,true),new(IPAddress.Loopback,100,IPAddress.Loopback,101)],
            [new("loopback",true,[IPAddress.Loopback]),new("ethernet",false,[Local])]);
        Assert.Equal(2,plan.Count);
        Assert.Equal("tcp and src port 13328",plan["ethernet"]);
        Assert.Equal("tcp and src host 127.0.0.1 and src port 56549",plan["loopback"]);
    }
    [Fact] public void ReusedTcpTupleStartsFreshDecoder()
    {
        var pipe=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};var received=0;
        pipe.Parser.Tap=(_,op,_)=>{if(op==0x0D90)received++;};
        pipe.OnSegment(0,Flow,99,true,false,[]);pipe.OnSegment(1,Flow,100,false,false,Data);
        pipe.OnSegment(2,Flow,999,true,false,[]);pipe.OnSegment(3,Flow,1000,false,false,Data);
        Assert.Equal(2,received);Assert.Equal(0,pipe.Gaps);
    }
    [Fact] public void DuplicateCaptureDoesNotDoubleApplyMessages()
    {
        var pipe=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};var received=0;
        pipe.Parser.Tap=(_,op,_)=>{if(op==0x0D90)received++;};
        pipe.OnSegment(0,Flow,99,true,false,[]);
        pipe.OnSegment(1,Flow,100,false,false,Data);pipe.OnSegment(2,Flow,100,false,false,Data);
        Assert.Equal(1,received);
    }
    [Fact] public void FragmentedHeartbeatStillLocksAndDelivers()
    {
        var pipe=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};var received=0;
        pipe.Parser.Tap=(_,op,_)=>{if(op==0x0D90)received++;};
        pipe.OnSegment(0,Flow,99,true,false,[]);
        var bytes=Data;for(var i=0;i<bytes.Length;i++)pipe.OnSegment(i+1,Flow,(uint)(100+i),false,false,bytes.AsSpan(i,1));
        Assert.Equal(1,received);
    }
    [Fact] public void QuietFlowPreservesPartialFrameAcrossPruneInterval()
    {
        var pipe=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};var received=0;
        pipe.Parser.Tap=(_,op,_)=>{if(op==0x0D90)received++;};
        pipe.OnSegment(0,Flow,99,true,false,[]);pipe.OnSegment(1,Flow,100,false,false,Data.AsSpan(0,13));
        pipe.OnSegment(121000,Flow,113,false,false,Data.AsSpan(13));Assert.Equal(1,received);
    }
    [Fact] public void InvalidFrameReportsLostContinuity()
    {
        var reasons=new List<string>();var decoder=new FrameDecoder((_,_)=>{});decoder.DataLost+=reasons.Add;
        decoder.Feed(new byte[]{1,1,1,1,1});Assert.NotEmpty(reasons);
    }

    [Fact] public void CompactHeartbeatSplitAcrossPacketsLocksStream()
    {
        var pipe=new PacketPipeline(GameData.Empty,_=>{}){LockThreshold=1};
        pipe.OnSegment(0,Flow,9,true,false,[]);
        pipe.OnSegment(1,Flow,10,false,false,new byte[]{6,0});
        pipe.OnSegment(2,Flow,12,false,false,new byte[]{54});
        Assert.Equal(1,pipe.GameFlows);
    }

    [Fact] public void ReadyVpnIsPreopenedBeforeGameChangesRoute()
    {
        var vpn=IPAddress.Parse("10.9.0.2");
        var plan=CapturePlan.Create([new(Local,1,Server,13328)], [new("ethernet",false,[Local]),new("vpn",false,[vpn]),new("disconnected",false,[IPAddress.Parse("169.254.2.3")])]);
        Assert.Contains("vpn",plan.Keys);Assert.DoesNotContain("disconnected",plan.Keys);
    }

    [Fact] public void NewConnectionPacketsWaitForGameProcessOwnership()
    {
        var delivered=new List<SegmentReader.Segment>();var gate=new GameConnectionGate(delivered.Add);
        gate.Push(new(1000,Flow,1,true,false,[]));gate.Push(new(1001,Flow,2,false,false,Data));
        Assert.Empty(delivered);
        gate.Update([new(Local,50000,Server,13328)],2000);
        Assert.Equal(2,delivered.Count);Assert.Equal(0,gate.PendingCount);Assert.True(delivered[0].Syn);
    }
    [Fact] public void UnrelatedProcessTrafficIsNeverDecoded()
    {
        var delivered=new List<SegmentReader.Segment>();var gate=new GameConnectionGate(delivered.Add);
        gate.Push(new(1000,Flow,1,false,false,Data));gate.Update([],12000);
        Assert.Empty(delivered);Assert.Equal(1,gate.Discarded);
    }
    [Fact] public void UnknownConnectionBufferIsBounded()
    {
        var gate=new GameConnectionGate(_=>throw new Exception("Unexpected decode"));
        for(var i=0;i<5000;i++)gate.Push(new(i,Flow,(uint)i,true,false,[]));
        Assert.Equal(4096,gate.PendingCount);Assert.True(gate.Discarded>0);
    }
}
