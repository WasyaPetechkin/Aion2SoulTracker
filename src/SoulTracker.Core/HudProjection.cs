namespace SoulTracker.Core;

public sealed record HudTargets(IReadOnlyList<PetProgress> Visible,int ActiveCount,int CompletedCount,int ExtraCount);

public static class HudProjection
{
    public static HudTargets Select(CollectionView view,IEnumerable<int> selectedIds,int limit=6)
    {
        var selected=selectedIds.ToHashSet();
        if(!view.HasSnapshot) return new([],0,0,0);
        var pinned=view.Pets.Where(x=>x.IsRegular && selected.Contains(x.Id)).ToArray();
        var active=pinned.Where(x=>!x.Completed).OrderBy(x=>x.Remaining ?? int.MaxValue).ThenBy(x=>x.Id).ToArray();
        var visible=active.Take(Math.Clamp(limit,1,10)).ToArray();
        return new(visible,active.Length,pinned.Count(x=>x.Completed),active.Length-visible.Length);
    }
}
