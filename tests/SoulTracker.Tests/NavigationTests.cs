using System.Buffers.Binary;
using System.Text.Json;
using AionMeter.Core.Game;
using SoulTracker.Core;

namespace SoulTracker.Tests;
public class NavigationTests
{
    private static string Root
    {
        get
        {
            var directory=new DirectoryInfo(AppContext.BaseDirectory);
            while(directory is not null && !Directory.Exists(Path.Combine(directory.FullName,"assets")))directory=directory.Parent;
            return directory!.FullName;
        }
    }
    private static readonly byte[] Entry=Convert.FromHexString("010000005604000094947c0000000000be18fc470125afc70090b246ea1e84420000000000000000000000000000");
    private static readonly byte[] Cast=Convert.FromHexString("8a650062efd5000e02aad103c4be8ac2160104c8e1bce7c5c2a2be46ec5f0100");
    [Fact]
    public void ActualEntryAndAttackLocateOnlyTheIdentifiedPlayerInSpawnCoordinates()
    {
        var state=new NavigationState();
        Assert.True(state.Process(100,0x2136,Entry));
        Assert.Equal(1110,state.View().WorldId);
        Assert.False(state.Process(101,0x0238,Cast)); // Identity is required for actor packets.
        state.Process(102,0x3336,Convert.FromHexString("8a65"));
        Assert.True(state.Process(103,0x0238,Cast));
        var position=state.View();
        Assert.Equal((uint)12938,position.ActorId);
        Assert.Equal(-135172.34375,position.X!.Value,3);
        Assert.Equal(7415.60986328125,position.Y!.Value,3); // Y is inverted to match the map data.
        Assert.Equal("attack",position.Kind);
        Assert.Equal(103,position.TimeMs);
        var other=Cast.ToArray();other[0]=0x8b;
        Assert.False(state.Process(104,0x0238,other));
        Assert.Equal(position,state.View());
    }
    [Fact]
    public void NpcMovementAndDamageNeverMoveThePlayerMarker()
    {
        var state=new NavigationState();state.Process(100,0x2136,Entry);state.Process(101,0x3336,Convert.FromHexString("8a65"));
        var original=state.View();
        var move=new byte[16];move[0]=1;
        WriteCoordinates(move,3,10000,20000,30000);
        Assert.False(state.Process(102,0x1a37,move));
        Assert.False(state.Process(103,0x0338,Cast));
        Assert.Equal(original,state.View());
        // An explicitly identified local movement packet can update the same position.
        move[0]=0x8a;move[1]=0x65;WriteCoordinates(move,4,10000,20000,30000);
        Assert.True(state.Process(104,0x1a37,move));
        Assert.Equal(-20000,state.View().Y);
    }
    [Theory]
    [InlineData(0x1C37,"AA1D05039D07E0C7D86E15C7446BCB46CEFDCEFD",-114703.2265625,38254.84375)]
    [InlineData(0x1C37,"ADD40104C8A9E2C745E40BC7006CCA46B6EDB6ED01",-116051.5625,35812.26953125)]
    [InlineData(0x1B37,"DC8E04044127EBC7A86504C7000EC3464B644B6401",-120398.5078125,33893.65625)]
    [InlineData(0x1B37,"D81B0503F999C9C7BA4B13C7C0B3C24639633963",-103219.9453125,37707.7265625)]
    public void ConditionalMovementFlagMatchesRecordedLayoutsAndStillRequiresLocalIdentity(int opcode,string hex,double x,double y)
    {
        var body=Convert.FromHexString(hex);
        var state=new NavigationState();state.Process(100,0x2136,Entry);
        state.Process(101,0x3336,Convert.FromHexString("8A65"));
        Assert.False(state.Process(102,(ushort)opcode,body)); // Actual captured actor is an NPC.
        // Bind that actor only inside this layout test; production never substitutes NPCs for the player.
        state.Process(103,0x3336,body);
        Assert.True(state.Process(104,(ushort)opcode,body));
        Assert.Equal(x,state.View().X!.Value,3);Assert.Equal(y,state.View().Y!.Value,3);
        var previous=state.View();
        Assert.False(state.Process(105,(ushort)opcode,body.AsSpan(0,12)));Assert.Equal(previous,state.View());
    }
    [Fact]
    public void TeleportAndResetNeverKeepAnotherZonesOrReplaysPositionAsLive()
    {
        var state=new NavigationState();state.Reset(replay:true);state.Process(100,0x2136,Entry);
        var teleport=new byte[18];WriteCoordinates(teleport,6,50000,-60000,24000);
        Assert.True(state.Process(101,0x2336,teleport));
        Assert.Equal(60000,state.View().Y);Assert.True(state.View().IsReplay);
        teleport[0]=1;Assert.False(state.Process(102,0x2336,teleport));
        state.Reset();Assert.False(state.View().HasPosition);Assert.Null(state.View().ActorId);Assert.False(state.View().IsReplay);
    }
    [Fact]
    public void StreamGapRetainsActorButRequiresZoneConfirmationBeforeShowingNewPosition()
    {
        var state=new NavigationState();state.Process(100,0x2136,Entry);state.Process(101,0x3336,Convert.FromHexString("8a65"));
        state.MarkGap();
        Assert.Equal((uint)12938,state.View().ActorId);Assert.Null(state.View().WorldId);Assert.False(state.View().HasPosition);
        Assert.True(state.Process(102,0x0238,Cast));Assert.False(state.View().HasPosition);
        var otherZone=Entry.ToArray();BinaryPrimitives.WriteInt32LittleEndian(otherZone.AsSpan(4),1010);
        Assert.True(state.Process(103,0x2136,otherZone));Assert.Equal(1010,state.View().WorldId);
        Assert.True(state.Process(104,0x0238,Cast));Assert.True(state.View().HasPosition);
        state.Reset();Assert.Null(state.View().ActorId);Assert.False(state.View().HasPosition);
    }
    [Fact]
    public void InvalidOrSelfBuffCoordinatesNeverReplaceConfirmedPosition()
    {
        var state=new NavigationState();state.Process(100,0x2136,Entry);state.Process(101,0x3336,Convert.FromHexString("8a65"));
        var original=state.View();
        for(var length=0;length<28;length++)Assert.False(state.Process(102,0x2136,Entry.AsSpan(0,length)));
        var invalid=Entry.ToArray();WriteCoordinates(invalid,16,float.NaN,10000,10000);
        Assert.False(state.Process(103,0x2136,invalid));
        var buff=Cast.ToArray();WriteCoordinates(buff,16,12,23,0);
        Assert.False(state.Process(104,0x0238,buff));
        Assert.Equal(original,state.View());
    }
    [Fact]
    public void ProjectionMatchesMapCornersAndCenterWithoutMirroringPlayerAndSpawns()
    {
        var world=new MapWorld("Altgard","asmodians","World_D_A",new double[]{-408000,-408000,8,8,102000},true);
        Assert.Equal((0d,0d),SpawnDatabase.Project(world,-408000,408000));
        Assert.Equal((1d,1d),SpawnDatabase.Project(world,408000,-408000));
        Assert.Equal((.5,.5),SpawnDatabase.Project(world,0,0));
    }
    private static void WriteCoordinates(byte[] bytes,int offset,float x,float y,float z)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset),x);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset+4),y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset+8),z);
    }
}
