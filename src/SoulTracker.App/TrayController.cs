using System.Windows;
using Forms=System.Windows.Forms;
namespace SoulTracker.App;

internal sealed class TrayController : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _image;
    private readonly Forms.ToolStripMenuItem _open,_hud,_map,_exit;
    private readonly Forms.ContextMenuStrip _menu;
    private bool _explained;
    internal bool Available=>_icon.Visible;
    internal TrayController(Action open,Action hud,Action map,Action exit)
    {
        using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"))!.Stream;
        using var original=new System.Drawing.Icon(stream);_image=(System.Drawing.Icon)original.Clone();
        _open=new();_hud=new();_map=new();_exit=new();_menu=new();
        _menu.Items.AddRange(new Forms.ToolStripItem[]{_open,_hud,_map,new Forms.ToolStripSeparator(),_exit});
        _open.Click+=(_,_)=>open();_hud.Click+=(_,_)=>hud();_map.Click+=(_,_)=>map();_exit.Click+=(_,_)=>exit();
        _icon=new(){Icon=_image,Text="Aion 2 Soul Tracker",ContextMenuStrip=_menu,Visible=true};
        _icon.DoubleClick+=(_,_)=>open();
    }
    internal void Configure(string language,bool hud,bool map)
    {
        var t=new UiText(language);
        _open.Text=t.Choose("Открыть трекер","Open tracker");_hud.Text=t.Choose("Счётчик душ","Soul counter");
        _map.Text=t.Choose("Интерактивная карта","Interactive map");_exit.Text=t.Choose("Выход","Exit");
        _hud.Checked=hud;_map.Checked=map;
    }
    internal void ExplainOnce(string language)
    {
        if(_explained)return;_explained=true;
        var t=new UiText(language);
        _icon.ShowBalloonTip(5000,"Aion 2 Soul Tracker",t.Choose("Трекер продолжает работать. Двойной щелчок по иконке открывает окно; «Выход» в меню закрывает программу.",
            "The tracker is still running. Double-click its icon to reopen; choose Exit from its menu to close it."),Forms.ToolTipIcon.Info);
    }
    public void Dispose(){_icon.Visible=false;_icon.Dispose();_menu.Dispose();_image.Dispose();}
}
