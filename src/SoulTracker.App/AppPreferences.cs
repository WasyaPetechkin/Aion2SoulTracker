using System.IO;
using System.Text.Json;
namespace SoulTracker.App;
internal sealed class AppPreferences
{
    public string Language {get;set;}="ru";
    public bool HudVisible {get;set;}=true;
    public bool HudLocked {get;set;}
    public double HudOpacity {get;set;}=.92;
    public double HudWidth {get;set;}=360;
    public double HudHeight {get;set;}=460;
    public double? HudLeft {get;set;}
    public double? HudTop {get;set;}
    public int[] HudTargets {get;set;}=[];
    public int HudRows {get;set;}=6;
    public double HudRowScale {get;set;}=1;
    public bool FocusMode {get;set;}
    public bool AutoTrackSouls {get;set;}
    public string CatalogTerritory {get;set;}="all";
    public string[] Categories {get;set;}=["Cogni","Fera","Natura","Varian"];
    public string HudHotkey {get;set;}="Ctrl+Alt+H";
    public string MapHotkey {get;set;}="Ctrl+Alt+M";
    public string LockHotkey {get;set;}="Ctrl+Alt+L";
    public bool MapVisible {get;set;}=true;
    public bool MapAllIncomplete {get;set;}
    public bool MapFollowPlayer {get;set;}=true;
    public int MapWorld {get;set;}=1110;
    public int MapPetId {get;set;}
    public double MapWidth {get;set;}=360;
    public double MapHeight {get;set;}=380;
    public bool MapSettingsExpanded {get;set;}
    public double? MapLeft {get;set;}
    public double? MapTop {get;set;}
    public double MapZoom {get;set;}=2;
    public static AppPreferences Load(string path)
    {
        try
        {
            var p=File.Exists(path) ? JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(path)) ?? new() : new();
            p.Language=p.Language=="en" ? "en" : "ru";
            if(p.CatalogTerritory is not ("all" or "asmodians" or "elyos" or "abyss" or "unknown"))p.CatalogTerritory="all";
            p.HudOpacity=Limit(p.HudOpacity,.25,1,.92);
            p.HudWidth=Limit(p.HudWidth,300,1600,360);p.HudHeight=Limit(p.HudHeight,220,1600,460);
            p.MapWidth=Limit(p.MapWidth,280,1600,360);p.MapHeight=Limit(p.MapHeight,300,1600,380);
            p.MapZoom=Limit(p.MapZoom,.75,20,2);p.HudRows=Math.Clamp(p.HudRows,1,10);
            p.HudRowScale=Limit(p.HudRowScale,.65,1.5,1);
            p.Categories=(p.Categories ?? ["Cogni","Fera","Natura","Varian"]).Where(x=>x is "Cogni" or "Fera" or "Natura" or "Varian").Distinct().ToArray();
            p.HudHotkey=HotkeySpec.NormalizeOrDefault(p.HudHotkey,"Ctrl+Alt+H");
            p.MapHotkey=HotkeySpec.NormalizeOrDefault(p.MapHotkey,"Ctrl+Alt+M");
            p.LockHotkey=HotkeySpec.NormalizeOrDefault(p.LockHotkey,"Ctrl+Alt+L");
            if(new[]{p.HudHotkey,p.MapHotkey,p.LockHotkey}.Distinct().Count()!=3)
            {p.HudHotkey="Ctrl+Alt+H";p.MapHotkey="Ctrl+Alt+M";p.LockHotkey="Ctrl+Alt+L";}
            p.HudLeft=Coordinate(p.HudLeft);p.HudTop=Coordinate(p.HudTop);
            p.MapLeft=Coordinate(p.MapLeft);p.MapTop=Coordinate(p.MapTop);
            p.HudTargets=(p.HudTargets ?? []).Where(x=>x>0).Distinct().Take(1000).ToArray();
            return p;
        }
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException) {return new();}
    }
    private static double Limit(double n,double min,double max,double fallback)=>double.IsFinite(n) ? Math.Clamp(n,min,max) : fallback;
    private static double? Coordinate(double? n)=>n.HasValue && double.IsFinite(n.Value) ? n : null;
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary=path+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(this,new JsonSerializerOptions {WriteIndented=true}));
        File.Move(temporary,path,true);
    }
}
