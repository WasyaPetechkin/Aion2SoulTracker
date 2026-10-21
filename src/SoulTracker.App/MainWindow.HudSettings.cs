using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace SoulTracker.App;

public partial class MainWindow
{
    private ContextMenu BuildHudQuickSettings()
    {
        var menu=new ContextMenu();
        MenuItem Toggle(CheckBox source,string topic)
        {
            var item=new MenuItem {Header=source.Content,Tag=source.Name,IsCheckable=true,StaysOpenOnClick=true,
                ToolTip=HelpText.Get(topic,_language)};
            // Share the actual collection controls: checked/unchecked handlers apply,
            // refresh and persist exactly the same settings from either window.
            item.SetBinding(MenuItem.IsCheckedProperty,new Binding(nameof(CheckBox.IsChecked))
                {Source=source,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
            return item;
        }
        menu.Items.Add(Toggle(FocusMode,"focus"));
        menu.Items.Add(Toggle(AutoTrackSouls,"autoTrack"));
        menu.Items.Add(Toggle(MapVisible,"map"));
        menu.Items.Add(new Separator());
        foreach(var category in new[]{Cogni,Fera,Natura,Varian})menu.Items.Add(Toggle(category,"category"));
        menu.Items.Add(new Separator());
        var catalog=new MenuItem {Header=Text.Choose("Фильтры каталога","Collection filters")};
        catalog.Items.Add(Toggle(Special,"special"));
        catalog.Items.Add(Toggle(HideCompleted,"max"));
        catalog.Items.Add(Toggle(OwnedOnly,"owned"));
        menu.Items.Add(catalog);
        var clickThrough=Toggle(HudLocked,"lock");clickThrough.StaysOpenOnClick=false;menu.Items.Add(clickThrough);
        var open=new MenuItem {Header=Text.Choose("Открыть каталог…","Open collection…")};
        open.Click+=(_,_)=>StartupWindow.ShowVisible(this);menu.Items.Add(open);
        // The app's global TextBlock style is light: keep native menu text readable.
        var textStyle=new Style(typeof(TextBlock));
        textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,SystemColors.MenuTextBrush));
        menu.Resources[typeof(TextBlock)]=textStyle;
        menu.Closed+=(_,_)=>
        {
            void Release(ItemsControl parent)
            {foreach(var child in parent.Items.OfType<MenuItem>()){BindingOperations.ClearAllBindings(child);Release(child);}}
            Release(menu);
        };
        return menu;
    }
}
