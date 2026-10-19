using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SoulTracker.Core;
namespace SoulTracker.App;
public sealed partial class MapCanvas
{
    private sealed record Kibelisk(int World,string Key,string Name,double X,double Y);
    private static readonly Kibelisk[] Kibelisks=LoadKibelisks();
    private readonly List<(Point Screen,Kibelisk Place)> _kibeliskHits=[];
    private readonly ToolTip _hoverTip=new(){Placement=PlacementMode.MousePoint,StaysOpen=true,MaxWidth=360};
    private string _hoverKey="";
    internal int KibeliskCount=>Kibelisks.Count(x=>x.Key==_world?.Key);
    internal IReadOnlyList<Point> SpawnScreenPoints=>_hits.Select(x=>x.Screen).ToArray();
    internal bool HoverTipVisible=>_hoverTip.IsOpen;
    private static Kibelisk[] LoadKibelisks()
    {
        var path=Path.Combine(AppContext.BaseDirectory,"maps","kibelisks.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<Kibelisk[]>(File.ReadAllText(path)) ?? [] : [];
    }
    private void RenderKibelisks(DrawingContext dc)
    {
        _kibeliskHits.Clear();
        if(_world is null)return;
        foreach(var k in Kibelisks.Where(x=>x.Key==_world.Key))
        {
            var (u,v)=SpawnDatabase.Project(_world,k.X,k.Y);var p=ToScreen(u,v);
            if(p.X<0 || p.Y<0 || p.X>ActualWidth || p.Y>ActualHeight)continue;
            var size=Zoom<2 ? 4.0 : 7.0;
            var geometry=new StreamGeometry();
            using(var ctx=geometry.Open())
            {ctx.BeginFigure(p+new Vector(0,-size),true,true);ctx.LineTo(p+new Vector(size*.75,0),true,false);ctx.LineTo(p+new Vector(0,size),true,false);ctx.LineTo(p+new Vector(-size*.75,0),true,false);}
            geometry.Freeze();dc.DrawGeometry(Brush("#3B245D"),new Pen(Brush("#F7D5FF"),1.3),geometry);
            if(Zoom>=2)DrawText(dc,"K",p,9,Brush("#FFFFFF"),true);_kibeliskHits.Add((p,k));
        }
    }
    internal void CloseTip(){_hoverTip.IsOpen=false;_hoverKey="";}
    private void ShowTip(string key,FrameworkElement content)
    {
        if(_hoverKey==key && _hoverTip.IsOpen)return;
        _hoverKey=key;_hoverTip.PlacementTarget=this;_hoverTip.Content=content;_hoverTip.IsOpen=true;
    }
    internal void HoverAt(Point p)
    {
        if(Locked){CloseTip();return;}
        var landmark=_kibeliskHits.Where(x=>(x.Screen-p).Length<10).OrderBy(x=>(x.Screen-p).Length).FirstOrDefault();
        if(landmark.Place is not null)
        {ShowTip("k:"+landmark.Place.Name,new TextBlock{Text=(_language=="ru" ? "Кибелиск" : "Kibelisk")+" · "+landmark.Place.Name,TextWrapping=TextWrapping.Wrap});return;}
        var hit=_hits.Where(h=>(h.Screen-p).Length<13).OrderBy(h=>(h.Screen-p).Length).FirstOrDefault();
        if(hit.Spawns is null){CloseTip();return;}
        var panel=new StackPanel();
        foreach(var spawn in hit.Spawns.DistinctBy(s=>(s.PetId,s.NpcId)).Take(6))
        {
            var pet=_pets[spawn.PetId];var row=new DockPanel{Margin=new Thickness(0,3,0,3)};
            row.Children.Add(new Image{Source=PetImages.Get(pet.Id),Width=32,Height=32,Margin=new Thickness(0,0,8,0)});
            row.Children.Add(new TextBlock{Text=$"{PetText.Name(pet,_language)} · {PetText.Remaining(pet,_language)} "+(_language=="ru" ? "до MAX" : "to MAX")+$"\n{spawn.NpcName} · Lv. {spawn.Level}",TextWrapping=TextWrapping.Wrap,MaxWidth=280});
            panel.Children.Add(row);
        }
        ShowTip(_language+":"+string.Join(',',hit.Spawns.Select(x=>x.PetId))+":"+_pets[hit.Spawns[0].PetId].Souls,panel);
    }
}
