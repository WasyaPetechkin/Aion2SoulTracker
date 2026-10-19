using AionMeter.Core.Capture;
namespace SoulTracker.Tests;
public sealed class TcpRecoveryTests
{
    [Fact] public void DelayedRetransmissionPreservesContinuity()
    {
        var bytes=new List<byte>();var gaps=0;
        var tcp=new TcpReassembler(data=>bytes.AddRange(data.ToArray()),()=>gaps++);
        tcp.Push(100,new byte[]{1,2},0);
        tcp.Push(104,new byte[]{5,6},100);
        tcp.Push(106,new byte[]{7,8},2000);
        tcp.Push(102,new byte[]{3,4},3000);
        Assert.Equal(0,gaps);Assert.Equal(Enumerable.Range(1,8).Select(x=>(byte)x),bytes);
    }
    [Fact] public void ActualLossStillSignalsUncertainty()
    {
        var gaps=0;var tcp=new TcpReassembler(_=>{},()=>gaps++);
        tcp.Push(100,new byte[]{1},0);tcp.Push(103,new byte[]{4},10);
        tcp.Push(104,new byte[]{5},TcpReassembler.GapTimeoutMs+11);
        Assert.Equal(1,gaps);
    }
}
