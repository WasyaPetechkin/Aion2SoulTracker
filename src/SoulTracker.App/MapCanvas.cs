using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SoulTracker.Core;

namespace SoulTracker.App;

/// <summary>Static spawn locations and the last confirmed player position, in one coordinate system.</summary>
public sealed partial class MapCanvas : FrameworkElement
{
    private readonly Dictionary<string,BitmapImage> _images=[];
    private readonly List<(Point Screen,SpawnPoint[] Spawns)> _hits=[];
    private MapWorld? _world;
    private IReadOnlyList<SpawnPoint> _points=[];
    private Dictionary<int,PetProgress> _pets=[];
    private NavigationView _position=new(null,null,null,null,0,"",false);
    private string _language="ru";
    private double _centerX=.5,_centerY=.5;
    private Point? _drag;
    private bool _fresh;
    private bool _locked;
    internal bool Locked {get=>_locked;set {_locked=value;if(value)CloseTip();}}
    internal double Zoom {get;private set;}=2;
    internal event Action? ViewChanged;
    internal event Action? Panned;
    internal int VisibleMarkerCount=>_hits.Count;
    internal NavigationView DisplayedPosition=>_position;
    private double Scale=>Math.Max(1,Math.Min(ActualWidth,ActualHeight))*Zoom;
    private Point ToScreen(double x,double y)=>new(ActualWidth/2+(x-_centerX)*Scale,ActualHeight/2+(y-_centerY)*Scale);

    public MapCanvas()
    {
        ClipToBounds=true;
        Focusable=false;
        MouseWheel+=(_,e)=>{if(Locked)return;ChangeZoom(Math.Pow(1.25,e.Delta/120.0),e.GetPosition(this));e.Handled=true;};
        MouseLeftButtonDown+=(_,e)=>{if(Locked)return;_drag=e.GetPosition(this);CaptureMouse();Cursor=Cursors.SizeAll;e.Handled=true;};
        MouseLeftButtonUp+=(_,e)=>{_drag=null;ReleaseMouseCapture();Cursor=Cursors.Arrow;};
        LostMouseCapture+=(_,_)=>_drag=null;
        MouseMove+=Move;
        MouseLeave+=(_,_)=>CloseTip();
        IsVisibleChanged+=(_,_)=>{if(!IsVisible)CloseTip();};
    }
    internal void SetWorld(MapWorld world)
    {
        if(_world?.Key==world.Key)return;
        _world=world;_centerX=_centerY=.5;InvalidateVisual();
    }
    internal void SetSpawns(IReadOnlyList<SpawnPoint> points,IReadOnlyList<PetProgress> pets,string language)
    {
        _points=points;_pets=pets.ToDictionary(x=>x.Id);_language=language;InvalidateVisual();
    }
    internal void UpdatePosition(NavigationView position,bool fresh,bool follow)
    {
        _position=position;_fresh=fresh;
        if(follow && position.HasPosition && _world is not null) CenterOn(position.X!.Value,position.Y!.Value);
        InvalidateVisual();
    }
    internal void CenterOn(double x,double y)
    {
        if(_world is null)return;
        (_centerX,_centerY)=SpawnDatabase.Project(_world,x,y);InvalidateVisual();
    }
    internal void SetZoom(double zoom) {Zoom=Math.Clamp(zoom,.75,20);InvalidateVisual();}
    internal void ZoomBy(double factor)=>ChangeZoom(factor,new(ActualWidth/2,ActualHeight/2));
    internal void Fit() {_centerX=_centerY=.5;Zoom=1;InvalidateVisual();Panned?.Invoke();ViewChanged?.Invoke();}
    private void ChangeZoom(double factor,Point anchor)
    {
        var before=Scale;
        var ax=_centerX+(anchor.X-ActualWidth/2)/before;
        var ay=_centerY+(anchor.Y-ActualHeight/2)/before;
        Zoom=Math.Clamp(Zoom*factor,.75,20);
        _centerX=ax-(anchor.X-ActualWidth/2)/Scale;
        _centerY=ay-(anchor.Y-ActualHeight/2)/Scale;
        InvalidateVisual();ViewChanged?.Invoke();
    }
    private void Move(object sender,MouseEventArgs e)
    {
        var p=e.GetPosition(this);
        if(_drag is Point previous)
        {
            _centerX=Math.Clamp(_centerX-(p.X-previous.X)/Scale,-.3,1.3);
            _centerY=Math.Clamp(_centerY-(p.Y-previous.Y)/Scale,-.3,1.3);
            _drag=p;CloseTip();InvalidateVisual();Panned?.Invoke();return;
        }
        HoverAt(p);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _hits.Clear();
        dc.DrawRectangle(Brush("#0B131D"),null,new Rect(RenderSize));
        if(_world is null)return;
        var origin=ToScreen(0,0);
        var rect=new Rect(origin,new Size(Scale,Scale));
        var path=Path.Combine(AppContext.BaseDirectory,"maps",_world.Key+".png");
        if(!_images.TryGetValue(path,out var bitmap) && File.Exists(path))
        {
            bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=new Uri(path);
            bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();bitmap.Freeze();_images[path]=bitmap;
        }
        if(bitmap is not null)dc.DrawImage(bitmap,rect);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(42,4,11,19)),null,new Rect(RenderSize));
        var gridPen=new Pen(new SolidColorBrush(Color.FromArgb(26,181,207,222)),1);
        for(var i=0;i<=8;i++)
        {
            var q=i/8.0;
            dc.DrawLine(gridPen,ToScreen(q,0),ToScreen(q,1));dc.DrawLine(gridPen,ToScreen(0,q),ToScreen(1,q));
        }
        var visible=_points.Select(s=>
        {
            var (u,v)=SpawnDatabase.Project(_world,s.X,s.Y);return (Spawn:s,Point:ToScreen(u,v));
        }).Where(p=>p.Point.X>=0 && p.Point.Y>=0 && p.Point.X<=ActualWidth && p.Point.Y<=ActualHeight);
        // Screen-space clustering keeps all-incomplete mode useful even with thousands of locations.
        foreach(var cluster in visible.GroupBy(p=>((int)(p.Point.X/17),(int)(p.Point.Y/17))))
        {
            var entries=cluster.ToArray();var spawns=entries.Select(x=>x.Spawn).ToArray();
            var center=new Point(entries.Average(x=>x.Point.X),entries.Average(x=>x.Point.Y));
            var genus=_pets[spawns[0].PetId].Genus;
            var color=CategoryBrush(genus);
            var radius=entries.Length>1 ? 6.5 : 4.2;
            dc.DrawEllipse(Brush("#0C1622"),null,center,radius+1.5,radius+1.5);
            dc.DrawEllipse(color,new Pen(Brush("#DAE4E8"),.7),center,radius,radius);
            if(entries.Length>1) DrawText(dc,entries.Length>99 ? "+" : entries.Length.ToString(),center,9,Brush("#0B1622"),true);
            _hits.Add((center,spawns));
        }
        RenderKibelisks(dc);
        if(_position.HasPosition)
        {
            var (u,v)=SpawnDatabase.Project(_world,_position.X!.Value,_position.Y!.Value);
            var player=ToScreen(u,v);var color=_fresh ? Brush("#72FFE0") : Brush("#FFCF78");
            dc.DrawEllipse(Brush("#0B1622"),new Pen(color,2),player,9,9);
            if(_fresh)dc.DrawEllipse(color,null,player,4,4);
            else
            {
                dc.DrawLine(new Pen(color,1.7),player+new Vector(-3,-3),player+new Vector(3,3));
                dc.DrawLine(new Pen(color,1.7),player+new Vector(-3,3),player+new Vector(3,-3));
            }
            DrawText(dc,_language=="ru" ? "Я" : "ME",player+new Vector(0,-21),11,color,true);
        }
        DrawText(dc,$"×{Zoom:0.0}",new Point(12,ActualHeight-21),11,Brush("#D5E3E9"),false);
    }
    private void DrawText(DrawingContext dc,string value,Point point,double size,Brush brush,bool centered)
    {
        var text=new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if(centered)point-=new Vector(text.Width/2,text.Height/2);
        dc.DrawText(text,point);
    }
    internal static Brush CategoryBrush(string genus)=>Brush(genus switch
    {"Cogni"=>"#F0BF7A","Fera"=>"#71D6EE","Natura"=>"#93DD8C","Varian"=>"#C6A0F0",_=>"#DEE7EC"});
    private static SolidColorBrush Brush(string color)
    {var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));b.Freeze();return b;}
}
