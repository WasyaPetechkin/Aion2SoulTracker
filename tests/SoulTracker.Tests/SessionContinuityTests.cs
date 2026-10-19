using AionMeter.Core.Capture;
using AionMeter.Core.Game;
using SoulTracker.Core;
namespace SoulTracker.Tests;
public sealed class SessionContinuityTests
{
    [Fact] public void QuietConnectionDoesNotDiscardTrustedProgress()
    {
        using var session=new TrackerSession([new(1120,"Pet","Fera","")],GameData.Empty);
        session.State.Process(100,PetProtocol.SnapshotOpcode,Convert.FromHexString("0000000001600400006004000002000000016004000048000000"));
        session.ObserveCaptureStatus(new(CaptureState.Capturing,"Connected"));
        session.ObserveCaptureStatus(new(CaptureState.Idle,"Quiet connection"));
        session.ObserveCaptureStatus(new(CaptureState.Capturing,"Connected"));
        Assert.False(session.State.View().NeedsResync);
        Assert.True(session.State.Process(110,PetProtocol.GainOpcode,Convert.FromHexString("016004000001000000")));
        Assert.Equal(73,session.State.View().Pets[0].Souls);
    }
    [Fact] public void RealDisconnectStillInvalidatesContinuity()
    {
        using var session=new TrackerSession([],GameData.Empty);
        session.ObserveCaptureStatus(new(CaptureState.Capturing,"Connected"));
        session.ObserveCaptureStatus(new(CaptureState.WaitingForGame,"Game closed"));
        Assert.True(session.State.View().NeedsResync);
    }
}
