using System.Text.Json;
using System.Text.Json.Nodes;
using AionMeter.Core.Game;
using SoulTracker.Core;

namespace SoulTracker.Tests;

public class CollectionPersistenceTests
{
    private static readonly PetDefinition[] Catalog=[new(1120,"Young Kailin","Fera","")];
    private static readonly byte[] Snapshot=Convert.FromHexString("0000000001600400006004000002000000016004000048000000");

    private static CollectionState ConfirmedState()
    {
        var state=new CollectionState(Catalog);
        Assert.True(state.Process(100,PetProtocol.SnapshotOpcode,Snapshot));
        state.Identify(200,"First",1101);
        return state;
    }

    [Theory]
    [InlineData("Pets")]
    [InlineData("History")]
    [InlineData("Character")]
    [InlineData("PetEntry")]
    [InlineData("HistoryEntry")]
    [InlineData("LastTimeMs")]
    public void InvalidSavedFieldsNeverCrashOrPartiallyReplaceExistingCollection(string field)
    {
        using var session=new TrackerSession(Catalog,GameData.Empty);
        Assert.True(session.State.Process(100,PetProtocol.SnapshotOpcode,Snapshot));
        session.State.Identify(200,"First",1101);
        var original=JsonSerializer.Serialize(session.State.View());
        var invalid=JsonNode.Parse(original)!.AsObject();
        switch(field)
        {
            case "PetEntry":invalid["Pets"]!.AsArray().Add(null);break;
            case "HistoryEntry":invalid["History"]!.AsArray().Add(null);break;
            case "LastTimeMs":invalid[field]=long.MaxValue;break;
            default:invalid[field]=null;break;
        }
        var path=Path.Combine(Path.GetTempPath(),$"soul-tracker-restore-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path,invalid.ToJsonString());
            Assert.False(session.Restore(path));
            Assert.Equal(original,JsonSerializer.Serialize(session.State.View()));
        }
        finally {File.Delete(path);}
    }

    [Fact]
    public void PartialIdentityDoesNotEraseKnownCollectionOrServer()
    {
        var state=ConfirmedState();
        state.Identify(40_000,"",0);
        state.Identify(40_001,"First",0);
        var view=state.View();
        Assert.True(view.HasSnapshot);
        Assert.False(view.NeedsResync);
        Assert.Equal("First",view.Character);
        Assert.Equal(1101,view.ServerId);
        Assert.Equal(72,Assert.Single(view.Pets).Souls);
    }

    [Fact]
    public void FirstValidatedServerCompletesIdentityWithoutDiscardingItsCollection()
    {
        var state=new CollectionState(Catalog);
        Assert.True(state.Process(100,PetProtocol.SnapshotOpcode,Snapshot));
        state.Identify(200,"First",0);
        Assert.Null(state.View().ServerId);
        state.Identify(40_000,"First",1101);
        Assert.True(state.View().HasSnapshot);
        Assert.Equal(1101,state.View().ServerId);
    }

    [Fact]
    public void ConfirmedDifferentServerStillInvalidatesAnotherCharactersCollection()
    {
        var state=ConfirmedState();
        state.Identify(40_000,"First",1102);
        Assert.False(state.View().HasSnapshot);
        Assert.True(state.View().NeedsResync);
        Assert.Equal(0,Assert.Single(state.View().Pets).Souls);
    }

    [Fact]
    public void SavedCollectionRoundTripKeepsProgressButRequiresFreshSnapshotBeforeGains()
    {
        var path=Path.Combine(Path.GetTempPath(),$"soul-tracker-save-{Guid.NewGuid():N}.json");
        try
        {
            using(var original=new TrackerSession(Catalog,GameData.Empty))
            {
                Assert.True(original.State.Process(100,PetProtocol.SnapshotOpcode,Snapshot));
                original.State.Identify(200,"First",1101);
                original.Save(path);
            }
            using var restored=new TrackerSession(Catalog,GameData.Empty);
            Assert.True(restored.Restore(path));
            Assert.True(restored.State.View().NeedsResync);
            Assert.Equal(72,Assert.Single(restored.State.View().Pets).Souls);
            var gain=Convert.FromHexString("016004000003000000");
            Assert.False(restored.State.Process(300,PetProtocol.GainOpcode,gain));
            Assert.True(restored.State.Process(400,PetProtocol.SnapshotOpcode,Snapshot));
            Assert.True(restored.State.Process(500,PetProtocol.GainOpcode,gain));
            Assert.True(Assert.Single(restored.State.View().Pets).Completed);
        }
        finally {File.Delete(path);}
    }
}
