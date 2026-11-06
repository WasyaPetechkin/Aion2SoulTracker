using SoulTracker.Core;
namespace SoulTracker.Tests;

public sealed class HudSelectionTests
{
    private static PetProgress Pet(int id,int level,int souls,string genus="Fera")=>new(id,"Pet "+id,genus,"",level,souls);
    private static CollectionView View(params PetProgress[] pets)=>new(1,1,true,false,"Demo",null,"",pets,[]);

    [Fact] public void EveryPinnedIncompleteTargetStaysVisibleAfterAnotherTargetGains()
    {
        var before=View(Pet(1,2,70),Pet(2,2,70),Pet(3,2,74));
        Assert.Equal(new[]{1,2},HudProjection.Select(before,[1,2]).Visible.Select(x=>x.Id));
        var after=before with {Pets=[Pet(1,2,70),Pet(2,2,71),Pet(3,2,74)]};
        Assert.Equal(new[]{2,1},HudProjection.Select(after,[1,2]).Visible.Select(x=>x.Id));
    }

    [Fact] public void CompletedPinDoesNotSelectAnUnpinnedReplacement()
    {
        var targets=HudProjection.Select(View(Pet(1,3,0),Pet(2,2,74)),[1]);
        Assert.Empty(targets.Visible);
        Assert.Equal(1,targets.CompletedCount);
        Assert.Equal(0,targets.ActiveCount);
    }

    [Fact] public void EmptySelectionOnlyExpandsTheMap()
    {
        var collection=View(Pet(1,2,74),Pet(2,2,50,"Cogni"));
        Assert.Empty(HudProjection.Select(collection,[]).Visible);
        Assert.Equal(new[]{1},SpawnDatabase.FilterTargets(collection,[],false,["Fera"]).Select(x=>x.Id));
    }

    [Fact] public void RowLimitReportsAdditionalPinnedTargets()
    {
        var targets=HudProjection.Select(View(Pet(1,2,70),Pet(2,2,74),Pet(3,1,20)),[1,2,3],1);
        Assert.Equal(2,Assert.Single(targets.Visible).Id);
        Assert.Equal(3,targets.ActiveCount);
        Assert.Equal(2,targets.ExtraCount);
    }
}
