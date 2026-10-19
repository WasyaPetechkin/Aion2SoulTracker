using System.Text.Json;
using SoulTracker.Core;
namespace SoulTracker.Tests;

public sealed class MapCategoryTests : IDisposable
{
    private readonly string _path=Path.GetTempFileName();
    private readonly SpawnDatabase _database;
    private readonly CollectionView _collection=new(100,100,true,false,"Test",null,"",new PetProgress[]
    {
        new(1,"Fera incomplete","Fera","",2,12),
        new(2,"Fera MAX","Fera","",3,0),
        new(3,"Cogni unowned","Cogni","",0,0),
        new(4,"Natura incomplete","Natura","",1,3),
        new(5,"Varian incomplete","Varian","",2,20),
        new(6,"Special","Special","",1,0),
        new(7,"Fera missing coordinates","Fera","",2,10)
    },Array.Empty<string>());
    public MapCategoryTests()
    {
        var data=new SpawnDatabase
        {
            Worlds=new() {[1110]=new("Test","asmodians","Test",new double[]{0,0,1,1,100},true)},
            Pets=Enumerable.Range(1,6).ToDictionary(id=>id,id=>new List<SoulDropper>
            {
                new(id,"NPC",1,1,new[]{new SpawnWorld(1110,null,new[]{new double[]{id*10,id*20}})})
            })
        };
        File.WriteAllText(_path,JsonSerializer.Serialize(data));_database=SpawnDatabase.Load(_path);
    }
    private int[] Selected(int[] pins,string[] categories,bool all=false,CollectionView? view=null)=>
        _database.Select(1110,view ?? _collection,pins,all,categories).Select(p=>p.PetId).Order().ToArray();
    [Fact] public void EmptySelectionShowsAllIncompleteFromEnabledCategories()
        =>Assert.Equal(new[]{1,3,4,5},Selected([], ["Fera","Cogni","Natura","Varian"]));
    [Theory]
    [InlineData("Fera",1)] [InlineData("Cogni",3)] [InlineData("Natura",4)] [InlineData("Varian",5)]
    public void EachCategoryWorksWithoutPinnedTargets(string category,int expected)
        =>Assert.Equal(new[]{expected},Selected([], [category]));
    [Fact] public void PinsNarrowTheMapAndRemovingLastPinRestoresCategories()
    {
        Assert.Equal(new[]{1,3},Selected([], ["Fera","Cogni"]));
        Assert.Equal(new[]{3},Selected([3], ["Fera","Cogni"]));
        Assert.Equal(new[]{1,3},Selected([], ["Fera","Cogni"]));
    }
    [Fact] public void PinsStillRespectDisabledCategories()
        =>Assert.Empty(Selected([3], ["Fera"]));
    [Fact] public void CompletedPinsDoNotExpandIntoOtherMobs()
        =>Assert.Empty(Selected([2], ["Fera"]));
    [Fact] public void ExplicitAllModeCanKeepHudPinsWhileBrowsingCategories()
        =>Assert.Equal(new[]{1,3},Selected([3], ["Fera","Cogni"],all:true));
    [Fact] public void NoEnabledCategoriesMeansNoPoints()
        =>Assert.Empty(Selected([], []));
    [Fact] public void UnknownCollectionAndSpecialPetsNeverFabricateMapTargets()
    {
        Assert.Empty(Selected([], ["Fera"],view:_collection with {HasSnapshot=false}));
        Assert.Empty(Selected([], ["Special"]));
    }
    [Fact] public void ReachingMaxRemovesTheTargetWithoutOtherFilterChanges()
    {
        Assert.Equal(new[]{1},Selected([], ["Fera"]));
        var completed=_collection with {Pets=_collection.Pets.Select(x=>x.Id==1 ? x with {Level=3, Souls=0} : x).ToArray()};
        Assert.Empty(Selected([], ["Fera"],view:completed));
    }
    [Fact] public void MissingCoordinateSummaryUsesTheSameAutomaticFilter()
    {
        var eligible=SpawnDatabase.FilterTargets(_collection,[],false,["Fera"]);
        Assert.Equal(new[]{1,7},eligible.Select(p=>p.Id));
        Assert.Equal(new[]{1},Selected([], ["Fera"]));
    }
    public void Dispose()=>File.Delete(_path);
}
