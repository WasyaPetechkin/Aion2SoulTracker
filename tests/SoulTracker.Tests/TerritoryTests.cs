using SoulTracker.Core;
namespace SoulTracker.Tests;
public sealed class TerritoryTests
{
    private static SpawnDatabase Database()=>new()
    {
        Worlds=new(){[1]=new("Altgard","asmodians","",[],true),[2]=new("Verteron","elyos","",[],true)},
        Pets=new(){[100]=[new(1,"Mob",1,1,[new(1,null,[[1,2]]),new(2,null,[[3,4]])])],
            [101]=[new(2,"Unknown",1,1,[new(1,null,[])])]}
    };
    [Fact] public void SharedPetMatchesBothTerritories()
    {var db=Database();Assert.True(db.MatchesTerritory(100,"asmodians"));Assert.True(db.MatchesTerritory(100,"elyos"));Assert.False(db.MatchesTerritory(100,"abyss"));}
    [Fact] public void MissingOrEmptyPointsAreUnknown()
    {var db=Database();Assert.True(db.MatchesTerritory(999,"unknown"));Assert.True(db.MatchesTerritory(101,"unknown"));Assert.Equal("Территория неизвестна",db.Territories(101,"ru"));Assert.False(db.MatchesTerritory(100,"unknown"));}
    [Fact] public void AllFilterIncludesUnknownAndLabelsDoNotNeedProgress()
    {var db=Database();Assert.True(db.MatchesTerritory(999,"all"));Assert.Contains("Asmo",db.Territories(100,"en"));Assert.Contains("Elyos",db.Territories(100,"en"));Assert.Contains("Altgard",db.LocationNames(100,"en"));}
}
