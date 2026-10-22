using System.Text.Json;

namespace SoulTracker.Core;

public sealed record MapWorld(string Name,string Group,string Key,double[] Sectors,bool Image);
public sealed record SpawnWorld(int World,string? Area,double[][] Points);
public sealed record SoulDropper(int Npc,string Name,int Level,double Chance,IReadOnlyList<SpawnWorld> Worlds);
public sealed record SpawnPoint(int PetId,int NpcId,string NpcName,int Level,int WorldId,string? Area,double X,double Y);
public sealed class SpawnDatabase
{
    public string Source {get;set;}="";
    public string Fetched {get;set;}="";
    public Dictionary<int,MapWorld> Worlds {get;set;}=[];
    public Dictionary<int,List<SoulDropper>> Pets {get;set;}=[];
    private Dictionary<int,SpawnPoint[]> _byWorld=[];
    public static SpawnDatabase Load(string path)
    {
        var db=JsonSerializer.Deserialize<SpawnDatabase>(File.ReadAllText(path),new JsonSerializerOptions {PropertyNameCaseInsensitive=true})!;
        var points=from pet in db.Pets from dropper in pet.Value from world in dropper.Worlds
                   where db.Worlds.ContainsKey(world.World) from p in world.Points
                   where p.Length>=2 && double.IsFinite(p[0]) && double.IsFinite(p[1])
                   select new SpawnPoint(pet.Key,dropper.Npc,dropper.Name,dropper.Level,world.World,world.Area,p[0],p[1]);
        db._byWorld=points.GroupBy(x=>x.WorldId).ToDictionary(x=>x.Key,x=>x.ToArray());
        return db;
    }
    public IReadOnlyList<SpawnPoint> Select(int world,CollectionView collection,IEnumerable<int> selected,bool allIncomplete,IEnumerable<string> genera,bool autoCategoriesWhenEmpty=true)
    {
        if(!collection.HasSnapshot || !_byWorld.TryGetValue(world,out var points)) return [];
        var eligible=FilterTargets(collection,selected,allIncomplete,genera,autoCategoriesWhenEmpty).Select(x=>x.Id).ToHashSet();
        return points.Where(x=>eligible.Contains(x.PetId)).ToArray();
    }
    public static IReadOnlyList<PetProgress> FilterTargets(CollectionView collection,IEnumerable<int> selected,bool allIncomplete,IEnumerable<string> genera,bool autoCategoriesWhenEmpty=true)
    {
        if(!collection.HasSnapshot)return [];
        var pins=selected.ToHashSet();var categories=genera.ToHashSet();
        // An empty explicit selection means browsing categories. A completed or filtered-out
        // pinned target still counts as selected, so it never expands into unrelated targets.
        var byCategory=allIncomplete || (autoCategoriesWhenEmpty && pins.Count==0);
        return collection.Pets.Where(x=>x.IsRegular && !x.Completed && categories.Contains(x.Genus) &&
            (byCategory || pins.Contains(x.Id))).ToArray();
    }
    public int PetCoverage=>Pets.Count(x=>x.Value.Any(d=>d.Worlds.Any(w=>w.Points.Length>0)));
    public HudTargets Focus(CollectionView collection,IEnumerable<string> categories,int? world)
    {
        var ids=world.HasValue && _byWorld.TryGetValue(world.Value,out var points) ? points.Select(x=>x.PetId).ToHashSet() : [];
        return HudProjection.Focus(collection with {Pets=collection.Pets.Where(x=>ids.Contains(x.Id)).ToArray()},categories);
    }
    public string Territories(int petId,string language)
    {
        var groups=TerritoryGroups(petId);
        if(groups.Length==0)return language=="ru" ? "Территория неизвестна" : "Territory unknown";
        return string.Join(" · ",groups.Select(x=>x switch {"asmodians"=>language=="ru" ? "◆ Асмо" : "◆ Asmo","elyos"=>language=="ru" ? "◇ Элийцы" : "◇ Elyos",_=>language=="ru" ? "◈ Бездна" : "◈ Abyss"}));
    }
    public string[] TerritoryGroups(int petId)=>Pets.TryGetValue(petId,out var drops)
        ? drops.SelectMany(x=>x.Worlds).Where(x=>x.Points.Length>0 && Worlds.ContainsKey(x.World))
            .Select(x=>Worlds[x.World].Group).Distinct().Order().ToArray() : [];
    public bool MatchesTerritory(int petId,string filter)
    {
        if(filter=="all")return true;
        var groups=TerritoryGroups(petId);
        return filter=="unknown" ? groups.Length==0 : groups.Contains(filter);
    }
    public string LocationNames(int petId,string language)=>Pets.TryGetValue(petId,out var drops)
        ? string.Join(", ",drops.SelectMany(x=>x.Worlds).Where(x=>x.Points.Length>0 && Worlds.ContainsKey(x.World))
            .Select(x=>Worlds[x.World].Name).Distinct()) : Territories(petId,language);
    public int PointCount=>_byWorld.Values.Sum(x=>x.Length);
    public static (double X,double Y) Project(MapWorld world,double x,double y)
    {
        var s=world.Sectors;
        return ((x-s[0])/(s[2]*s[4]),1+(s[1]-y)/(s[3]*s[4]));
    }
}
