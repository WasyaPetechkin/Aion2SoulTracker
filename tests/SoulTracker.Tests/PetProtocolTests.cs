using SoulTracker.Core;
namespace SoulTracker.Tests;
public class PetProtocolTests
{
    private static readonly PetDefinition[] Catalog=[new(1120,"Young Kailin","Fera","", "Малый каилин")];
    private static readonly byte[] Snapshot=Convert.FromHexString("0000000001600400006004000002000000016004000048000000");
    private static CollectionState Initial()
    {
        var state=new CollectionState(Catalog);
        Assert.True(state.Process(100,PetProtocol.SnapshotOpcode,Snapshot));return state;
    }
    [Fact] public void GainReachesMaximumAndRemovesHudTarget()
    {
        var state=Initial();Assert.Equal(3,Assert.Single(HudProjection.Select(state.View(),new[]{1120}).Visible).Remaining);
        Assert.True(state.Process(101,PetProtocol.GainOpcode,Convert.FromHexString("016004000003000000")));
        var pet=Assert.Single(state.View().Pets);Assert.True(pet.Completed);Assert.Equal(0,pet.Souls);
        Assert.Empty(HudProjection.Select(state.View(),new[]{1120}).Visible);
    }
    [Fact] public void TruncationDoesNotCorruptTheCollection()
    {
        var state=Initial();
        for(var size=0;size<Snapshot.Length;size++)
        {Assert.False(state.Process(101,PetProtocol.SnapshotOpcode,Snapshot.AsSpan(0,size)));Assert.Equal(72,Assert.Single(state.View().Pets).Souls);}
    }
    [Fact] public void GapBlocksGainsUntilNewSnapshot()
    {
        var state=Initial();state.MarkUncertain("Test gap");
        var gain=Convert.FromHexString("016004000001000000");
        Assert.False(state.Process(101,PetProtocol.GainOpcode,gain));
        Assert.True(state.Process(102,PetProtocol.SnapshotOpcode,Snapshot));
        Assert.True(state.Process(103,PetProtocol.GainOpcode,gain));Assert.Equal(73,Assert.Single(state.View().Pets).Souls);
    }
    [Fact] public void SearchMatchesBothLanguagesAndId()
    {
        var pet=Assert.Single(Initial().View().Pets);
        Assert.True(PetText.Matches(pet,"малый"));Assert.True(PetText.Matches(pet,"KAILIN"));Assert.True(PetText.Matches(pet,"1120"));
    }
}
