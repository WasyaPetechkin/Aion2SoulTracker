using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace SoulTracker.App;

public partial class MainWindow
{
    private sealed class HudContextMenu : ContextMenu
    {
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if(e.Property!=IsOpenProperty || e.NewValue is not false)return;
            // Popup.Closed may be delayed by animation; close help immediately.
            void CloseHelp(ItemsControl parent)
            {
                foreach(var item in parent.Items.OfType<MenuItem>())
                {if(item.ToolTip is ToolTip tip)tip.IsOpen=false;CloseHelp(item);}
            }
            CloseHelp(this);
        }
    }
    private ContextMenu BuildHudQuickSettings()
    {
        var menu=new HudContextMenu {Background=System.Windows.Media.Brushes.WhiteSmoke,Foreground=System.Windows.Media.Brushes.Black};
        MenuItem Toggle(CheckBox source,string topic)
        {
            var item=new MenuItem {Header=source.Content,Tag=source.Name,IsCheckable=true,StaysOpenOnClick=true};
            var tip=new ToolTip {Content=new TextBlock {Text=HelpText.Get(topic,_language),TextWrapping=TextWrapping.Wrap,MaxWidth=300},
                PlacementTarget=item,Placement=System.Windows.Controls.Primitives.PlacementMode.Right,IsHitTestVisible=false};
            item.ToolTip=tip;
            ToolTipService.SetInitialShowDelay(item,450);
            ToolTipService.SetShowDuration(item,8000);
            item.MouseLeave+=(_,_)=>tip.IsOpen=false;
            item.Click+=(_,_)=>tip.IsOpen=false;
            item.Unloaded+=(_,_)=>tip.IsOpen=false;
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
        // Generated native menu headers can still pick up App.xaml's light implicit
        // TextBlock style. Set the actual header foreground locally, including submenus.
        void StyleHeaders(ItemsControl parent)
        {
            foreach(var item in parent.Items.OfType<MenuItem>())
            {
                item.Foreground=System.Windows.Media.Brushes.Black;
                item.Background=System.Windows.Media.Brushes.WhiteSmoke;
                item.Header=new TextBlock {Text=item.Header?.ToString(),Foreground=System.Windows.Media.Brushes.Black,FontSize=13};
                StyleHeaders(item);
            }
        }
        StyleHeaders(menu);
        menu.Closed+=(_,_)=>
        {
            void Release(ItemsControl parent)
            {foreach(var child in parent.Items.OfType<MenuItem>()){if(child.ToolTip is ToolTip tip)tip.IsOpen=false;BindingOperations.ClearAllBindings(child);Release(child);}}
            Release(menu);
        };
        return menu;
    }
}
