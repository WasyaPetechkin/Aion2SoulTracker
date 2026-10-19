namespace SoulTracker.Core;

public sealed record HudTargets(IReadOnlyList<PetProgress> Visible,int ActiveCount,int CompletedCount,int ExtraCount);

public static class HudProjection
{
    public static HudTargets Focus(CollectionView view,IEnumerable<string> categories)
    {
        if(!view.HasSnapshot)return new([],0,0,0);
        var enabled=categories.ToHashSet();
        var eligible=view.Pets.Where(x=>x.IsRegular && enabled.Contains(x.Genus)).ToArray();
        var active=eligible.Where(x=>!x.Completed && x.Remaining.HasValue).ToArray();
        if(active.Length==0)return new([],0,eligible.Count(x=>x.Completed),0);
        var nearest=active.Min(x=>x.Remaining!.Value);
        // All ties are intentional, including when there are more leaders than normal HUD rows.
        var leaders=active.Where(x=>x.Remaining==nearest).OrderBy(x=>x.Id).ToArray();
        return new(leaders,active.Length,eligible.Count(x=>x.Completed),0);
    }
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
