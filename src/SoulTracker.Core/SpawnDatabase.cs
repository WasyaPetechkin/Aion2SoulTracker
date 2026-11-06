using System.Text.Json;

namespace SoulTracker.Core;

public sealed record MapWorld(string Name,string Group,string Key,double[] Sectors,bool Image);
public sealed record SpawnWorld(int World,string? Area,double[][] Points);
public sealed record SoulDropper(int Npc,string Name,int Level,double Chance,IReadOnlyList<SpawnWorld> Worlds,bool WorldBoss=false,string? NameRu=null);
public sealed record FarmingLocation(string Name,string? NameRu,string Group);
public sealed record AcquisitionItem(int Id,string Name,string? NameRu);
public sealed record SpawnPoint(int PetId,int NpcId,string NpcName,int Level,int WorldId,string? Area,double X,double Y);
public sealed class SpawnDatabase
{
    public string Source {get;set;}="";
    public string Fetched {get;set;}="";
    public Dictionary<int,MapWorld> Worlds {get;set;}=[];
    public Dictionary<int,List<SoulDropper>> Pets {get;set;}=[];
    // Location metadata is independent of map textures and coordinates.
    public Dictionary<int,FarmingLocation> Locations {get;set;}=[];
    public Dictionary<int,List<AcquisitionItem>> AcquisitionItems {get;set;}=[];
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
    public int PetCoverage=>Pets.Count(x=>x.Value.Any(d=>d.Worlds.Any(w=>Worlds.ContainsKey(w.World) && w.Points.Length>0)));
    public string Territories(int petId,string language)
    {
        var groups=TerritoryGroups(petId);
        if(groups.Length==0)return HasAcquisitionItems(petId) ? (language=="ru" ? "Сундуки" : "Chests") : (language=="ru" ? "Источник уточняется" : "Source not documented");
        if(groups.Length==1 && groups[0]=="dungeons")
            return string.Join(", ",KnownLocations(petId).Select(x=>LocationName(x,language)).Distinct());
        return string.Join(" · ",groups.Select(x=>x switch
        {"asmodians"=>language=="ru" ? "◆ Асмо" : "◆ Asmo","elyos"=>language=="ru" ? "◇ Элийцы" : "◇ Elyos",
         "abyss"=>language=="ru" ? "◈ Бездна" : "◈ Abyss","dungeons"=>language=="ru" ? "Подземелья" : "Dungeons",_=>x}));
    }
    public string[] TerritoryGroups(int petId)=>KnownLocations(petId).Select(x=>x.Group).Distinct().Order().ToArray();
    private IEnumerable<FarmingLocation> KnownLocations(int petId)
    {
        if(!Pets.TryGetValue(petId,out var drops))yield break;
        foreach(var id in drops.SelectMany(x=>x.Worlds).Select(x=>x.World).Distinct())
            if(Locations.TryGetValue(id,out var location))yield return location;
            else if(Worlds.TryGetValue(id,out var map))yield return new(map.Name,null,map.Group);
    }
    private static string LocationName(FarmingLocation location,string language)=>language=="ru" && !string.IsNullOrWhiteSpace(location.NameRu) ? location.NameRu : location.Name;
    public bool HasWorldBoss(int petId)=>Pets.TryGetValue(petId,out var drops) && drops.Any(x=>x.WorldBoss);
    public string WorldBossLabel(int petId,string language)=>!HasWorldBoss(petId) ? "" : language=="ru" ? "★ Мировой босс" : "★ World boss";
    public string WorldBossNames(int petId,string language)=>Pets.TryGetValue(petId,out var drops)
        ? string.Join(", ",drops.Where(x=>x.WorldBoss).Select(x=>language=="ru" && !string.IsNullOrWhiteSpace(x.NameRu) ? x.NameRu : x.Name).Distinct()) : "";
    private bool HasAcquisitionItems(int petId)=>AcquisitionItems.TryGetValue(petId,out var items) && items.Count>0;
    public bool MatchesTerritory(int petId,string filter)
    {
        if(filter=="all")return true;
        if(filter=="worldboss")return HasWorldBoss(petId);
        if(filter=="chests")return HasAcquisitionItems(petId);
        var groups=TerritoryGroups(petId);
        return filter=="other" ? groups.Length==0 && !HasAcquisitionItems(petId) : groups.Contains(filter);
    }
    public string LocationNames(int petId,string language)
    {
        var lines=KnownLocations(petId).Select(x=>LocationName(x,language)+(x.Group=="dungeons" ? (language=="ru" ? " · подземелье" : " · dungeon") : "")).Distinct().ToList();
        if(HasWorldBoss(petId))lines.Add(WorldBossLabel(petId,language)+": "+WorldBossNames(petId,language));
        if(AcquisitionItems.TryGetValue(petId,out var items) && items.Count>0)
        {
            lines.Add(language=="ru" ? "Получение питомца из сундуков:" : "Pet acquisition from chests:");
            lines.AddRange(items.Select(x=>language=="ru" && !string.IsNullOrWhiteSpace(x.NameRu) ? x.NameRu : x.Name));
        }
        return lines.Count==0 ? Territories(petId,language) : string.Join("\n",lines);
    }
    public int PointCount=>_byWorld.Values.Sum(x=>x.Length);
    public static (double X,double Y) Project(MapWorld world,double x,double y)
    {
        var s=world.Sectors;
        return ((x-s[0])/(s[2]*s[4]),1+(s[1]-y)/(s[3]*s[4]));
    }
}
