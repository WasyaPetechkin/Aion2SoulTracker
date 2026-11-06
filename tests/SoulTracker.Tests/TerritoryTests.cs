using System.Text.Json;
using SoulTracker.Core;
namespace SoulTracker.Tests;
public sealed class TerritoryTests
{
    private static SpawnDatabase Database()=>new()
    {
        Worlds=new(){[1]=new("Altgard","asmodians","",[],true),[2]=new("Verteron","elyos","",[],true)},
        Locations=new(){[600032]=new("Draupnir","Драупнир","dungeons")},
        Pets=new(){[100]=[new(1,"Mob",1,1,[new(1,null,[[1,2]]),new(2,null,[[3,4]])])],
            [101]=[new(2,"Mob without points",1,1,[new(1,null,[])])],
            [102]=[new(3,"Dungeon boss",45,1,[new(600032,null,[])])],
            [103]=[new(4,"World boss",45,1,[new(1,null,[])],true,"Мировой босс")],
            [104]=[new(5,"Missing map metadata",1,1,[new(9999,null,[[1,2]])])]},
        AcquisitionItems=new(){[105]=[new(99,"Pet Chest","Сундук питомцев")]}
    };
    [Fact] public void SharedPetMatchesBothTerritories()
    {var db=Database();Assert.True(db.MatchesTerritory(100,"asmodians"));Assert.True(db.MatchesTerritory(100,"elyos"));Assert.False(db.MatchesTerritory(100,"abyss"));}
    [Fact] public void KnownLocationDoesNotRequireCoordinatesOrTexture()
    {
        var db=Database();Assert.True(db.MatchesTerritory(101,"asmodians"));
        Assert.True(db.MatchesTerritory(102,"dungeons"));Assert.False(db.MatchesTerritory(102,"abyss"));
        Assert.Equal("Драупнир",db.Territories(102,"ru"));Assert.Equal("Draupnir",db.Territories(102,"en"));
        Assert.Contains("подземелье",db.LocationNames(102,"ru"));Assert.False(db.Worlds.ContainsKey(600032));
    }
    [Fact] public void DungeonBossIsNotWorldBoss()
    {var db=Database();Assert.False(db.MatchesTerritory(102,"worldboss"));Assert.Empty(db.WorldBossLabel(102,"ru"));Assert.True(db.MatchesTerritory(103,"worldboss"));Assert.Contains("Мировой босс",db.LocationNames(103,"ru"));}
    [Fact] public void ChestAcquisitionDoesNotInventNpcLocation()
    {var db=Database();Assert.True(db.MatchesTerritory(105,"chests"));Assert.False(db.MatchesTerritory(105,"dungeons"));Assert.Empty(db.TerritoryGroups(105));Assert.Equal("Сундуки",db.Territories(105,"ru"));Assert.Contains("Pet acquisition",db.LocationNames(105,"en"));}
    [Fact] public void MissingMetadataDoesNotDefaultToAbyssOrDungeons()
    {var db=Database();Assert.True(db.MatchesTerritory(104,"other"));Assert.False(db.MatchesTerritory(104,"abyss"));Assert.False(db.MatchesTerritory(104,"dungeons"));Assert.False(db.MatchesTerritory(105,"other"));}
    [Fact] public void AllFilterAndLabelsDoNotNeedProgress()
    {var db=Database();Assert.True(db.MatchesTerritory(999,"all"));Assert.Contains("Asmo",db.Territories(100,"en"));Assert.Contains("Elyos",db.Territories(100,"en"));Assert.Contains("Altgard",db.LocationNames(100,"en"));}
    [Fact] public void PackagedSourcesAccountForEveryRegularPet()
    {
        var db=SpawnDatabase.Load(Path.Combine(AppContext.BaseDirectory,"spawns.json"));
        using var catalog=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"pet-catalog.json")));
        var ids=catalog.RootElement.EnumerateArray().Where(x=>x.GetProperty("Genus").GetString()!="Special").Select(x=>x.GetProperty("Id").GetInt32()).ToArray();
        Assert.Equal(200,ids.Length);
        Assert.All(ids,id=>Assert.True(db.TerritoryGroups(id).Length>0 || db.MatchesTerritory(id,"chests"),$"Missing source: {id}"));
        Assert.All(db.Pets.Values.SelectMany(x=>x).SelectMany(x=>x.Worlds),w=>Assert.True(db.Locations.ContainsKey(w.World)));
        Assert.Equal(6,db.Worlds.Count);Assert.Equal(14634,db.PointCount);
    }
    [Fact] public void PackagedSaraswatiAndBasiliskHaveDocumentedSources()
    {
        var db=SpawnDatabase.Load(Path.Combine(AppContext.BaseDirectory,"spawns.json"));
        Assert.Equal("Драупнир",db.Territories(1215,"ru"));Assert.False(db.HasWorldBoss(1215));
        Assert.True(db.HasWorldBoss(1206));Assert.Contains("Ansas",db.WorldBossNames(1206,"en"));
        Assert.True(db.MatchesTerritory(1198,"chests"));Assert.Empty(db.TerritoryGroups(1198));
    }
}
