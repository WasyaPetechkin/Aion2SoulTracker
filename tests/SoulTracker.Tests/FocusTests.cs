using SoulTracker.Core;
namespace SoulTracker.Tests;

public sealed class FocusTests
{
    private static PetProgress Pet(int id,int level,int souls,string genus="Fera")=>new(id,"Pet "+id,genus,"",level,souls);
    private static CollectionView View(params PetProgress[] pets)=>new(1,1,true,false,"Demo",null,"",pets,[]);
    private static int[] Leaders(CollectionView v,params string[] categories)=>HudProjection.Focus(v,categories.Length==0 ? ["Fera","Cogni","Natura","Varian"] : categories).Visible.Select(x=>x.Id).ToArray();

    [Fact] public void TiesNarrowAfterGainAndAdvanceAfterMax()
    {
        Assert.Equal(new[]{1,2},Leaders(View(Pet(1,2,70),Pet(2,2,70),Pet(3,2,60))));
        Assert.Equal(new[]{2},Leaders(View(Pet(1,2,70),Pet(2,2,71),Pet(3,2,60))));
        Assert.Equal(new[]{1},Leaders(View(Pet(1,2,70),Pet(2,3,0),Pet(3,2,60))));
        Assert.Equal(new[]{3},Leaders(View(Pet(1,3,0),Pet(2,3,0),Pet(3,2,60))));
    }
    [Fact] public void UsesTotalRemainingRatherThanSoulsOnCurrentStage()
        =>Assert.Equal(new[]{2},Leaders(View(Pet(1,1,24),Pet(2,2,1),Pet(3,0,4))));
    [Fact] public void CategoriesExcludeCloserTargetsAndSpecialPets()
        =>Assert.Equal(new[]{1},Leaders(View(Pet(1,2,50),Pet(2,2,74,"Cogni"),Pet(3,2,74,"Special")),"Fera"));
    [Fact] public void AwaitingLevelConfirmationIsNotRankedAsZeroRemaining()
        =>Assert.Equal(new[]{2},Leaders(View(Pet(1,2,75),Pet(2,2,60))));
    [Fact] public void NoDataOrNoCategoriesNeverInventTargets()
    {
        Assert.Empty(Leaders(View(Pet(1,2,74)) with {HasSnapshot=false}));
        Assert.Empty(HudProjection.Focus(View(Pet(1,2,74)),[]).Visible);
        Assert.Empty(Leaders(View(Pet(1,3,0))));
    }
    [Fact] public void AllTiesSurviveNormalHudRowLimit()
        =>Assert.Equal(15,Leaders(View(Enumerable.Range(1,15).Select(x=>Pet(x,2,70)).ToArray())).Length);
    [Fact] public void FocusMapEmptySelectionDoesNotFallbackToCategories()
    {
        var v=View(Pet(1,2,75));
        var ids=Leaders(v);
        Assert.Empty(ids);
        Assert.Empty(SpawnDatabase.FilterTargets(v,ids,false,["Fera"],autoCategoriesWhenEmpty:false));
        Assert.Single(SpawnDatabase.FilterTargets(v,[],false,["Fera"]));
    }
    [Fact] public void MapAndHudShareTheExactLeaderSet()
    {
        var v=View(Pet(1,2,70),Pet(2,2,70),Pet(3,2,50));
        var ids=Leaders(v);
        Assert.Equal(ids,SpawnDatabase.FilterTargets(v,ids,false,["Fera"],false).Select(x=>x.Id));
    }
}
