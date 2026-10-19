using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SoulTracker.Core;
namespace SoulTracker.App;
public partial class MainWindow
{
    // Local, synthetic acceptance check. It never starts capture or changes normal user data.
    internal void Verify(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks=new Dictionary<string,bool>();
        void Check(string name,bool value)=>checks.Add(name,value);
        Check("fresh_collection_empty",!_session!.State.View().HasSnapshot && Pets.Items.Count==0);
        Check("packaged_maps",_spawns.Worlds.Count==6 && _spawns.PointCount==14634);
        Render(this,Path.Combine(directory,"first-run.png"));
        UpdatePrerequisite(false);Render(this,Path.Combine(directory,"npcap-required.png"));
        Check("missing_npcap_guidance",PrerequisitePanel.IsVisible && DownloadNpcap.Content is not null);
        UpdatePrerequisite();
        var initial=_session.State.View();
        var stamp=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var sample=initial with {HasSnapshot=true,SnapshotTimeMs=stamp,LastTimeMs=stamp,Character="Demo",NeedsResync=false,
            Pets=initial.Pets.Select(p=>p with {Level=p.IsRegular ? 2 : 0, Souls=p.IsRegular ? p.Id%70 : 0}).ToArray()};
        Check("synthetic_collection_loaded",_session.State.Restore(sample));
        Refresh(true);
        var automaticCount=_map.SpawnCount;
        Check("no_pins_shows_categories",automaticCount>0 && !_preferences.MapAllIncomplete && _preferences.HudTargets.Length==0);
        Cogni.IsChecked=false;Natura.IsChecked=false;Varian.IsChecked=false;
        var feraCount=_map.SpawnCount;
        Check("category_checkbox_filters_map",feraCount>0 && feraCount<automaticCount &&
            feraCount==_spawns.Select(_preferences.MapWorld,_session.State.View(),[],false,["Fera"]).Count);
        Search.Text="not-a-pet-name";
        Check("text_search_only_filters_catalog",Pets.Items.Count==0 && _map.SpawnCount==feraCount);
        Search.Clear();
        _map.Left=-10000;_map.Top=-10000;_map.Show();
        Render(_map,Path.Combine(directory,"map-category-fera.png"));
        Fera.IsChecked=false;
        Check("no_categories_clears_map",_map.SpawnCount==0);
        Cogni.IsChecked=Fera.IsChecked=Natura.IsChecked=Varian.IsChecked=true;
        Pets.UpdateLayout();
        var row=(DataGridRow)Pets.ItemContainerGenerator.ContainerFromIndex(0);
        var pin=FindVisual<CheckBox>(row)!;var pet=(PetRow)row.DataContext;
        pin.IsChecked=true;pin.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check("catalog_target_pin",_preferences.HudTargets.Contains(pet.Id));
        Check("pinned_target_narrows_map",_map.SpawnCount<automaticCount);
        Pets.UpdateLayout();
        var pinnedItem=Pets.Items.Cast<PetRow>().Single(x=>x.Id==pet.Id);
        var pinnedRow=(DataGridRow)Pets.ItemContainerGenerator.ContainerFromIndex(Pets.Items.IndexOf(pinnedItem));
        var currentPin=FindVisual<CheckBox>(pinnedRow)!;
        currentPin.IsChecked=false;currentPin.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check("last_unpin_restores_categories",_preferences.HudTargets.Length==0 && _map.SpawnCount==automaticCount);
        _preferences.HudTargets=[1120,1106,1066,1126];
        Refresh(true);
        _hud.Left=-10000;_hud.Top=-10000;_hud.Show();_hud.UpdateLayout();
        _map.Left=-10000;_map.Top=-10000;_map.Show();_map.UpdateLayout();
        var hudHandle=new WindowInteropHelper(_hud).Handle;var mapHandle=new WindowInteropHelper(_map).Handle;
        SetHudLock(true);
        Check("click_through_enabled",((long)OverlayNative.GetWindowLongPtr(hudHandle,-20)&OverlayNative.Transparent)!=0 &&
            ((long)OverlayNative.GetWindowLongPtr(mapHandle,-20)&OverlayNative.Transparent)!=0);
        var lockedWidth=_map.Width;
        _map.ResizeCorner.RaiseEvent(new DragDeltaEventArgs(40,40){RoutedEvent=Thumb.DragDeltaEvent});
        Check("locked_size_preserved",_map.Width==lockedWidth);
        SetHudLock(false);
        Check("editing_restored",((long)OverlayNative.GetWindowLongPtr(hudHandle,-20)&OverlayNative.Transparent)==0 &&
            ((long)OverlayNative.GetWindowLongPtr(mapHandle,-20)&OverlayNative.Transparent)==0);
        var width=_hud.Width;
        _hud.ResizeCorner.RaiseEvent(new DragDeltaEventArgs(30,30){RoutedEvent=Thumb.DragDeltaEvent});
        _hud.ResizeCorner.RaiseEvent(new DragCompletedEventArgs(30,30,false){RoutedEvent=Thumb.DragCompletedEvent});
        Check("hud_resize_saved",_hud.Width==width+30 && _preferences.HudWidth==_hud.Width);
        _map.AllIncomplete.IsChecked=false;var pins=_map.SpawnCount;
        _map.TargetPicker.SelectedValue=1120;
        Check("single_target_filter",_map.SpawnCount>0 && _map.SpawnCount<pins);
        _map.AllIncomplete.IsChecked=true;
        Check("all_incomplete_filter",_map.SpawnCount>pins);
        _map.WorldPicker.SelectedValue=1010;
        Check("territory_switch",_preferences.MapWorld==1010 && _map.SpawnCount>0 && !_preferences.MapFollowPlayer);
        var nav=new NavigationView(1010,42,-90000,40000,stamp,"attack",false);
        _map.RefreshNavigation(nav);
        Check("player_position",_map.Map.DisplayedPosition.HasPosition && _map.Map.DisplayedPosition.X==-90000);
        _map.RefreshNavigation(nav with {WorldId=1110});
        Check("other_territory_position_hidden",!_map.Map.DisplayedPosition.HasPosition);
        _map.RefreshNavigation(nav);
        _map.Map.ZoomBy(1.25);Check("map_zoom",_map.CurrentZoom>2);
        Render(this,Path.Combine(directory,"collection-ru.png"));
        Render(_hud,Path.Combine(directory,"hud-ru.png"));
        Render(_map,Path.Combine(directory,"map-ru.png"));
        VerifyDesktopControls(directory,checks,sample);
        LanguagePicker.SelectedIndex=1;
        Check("language_preserves_targets",_preferences.HudTargets.SequenceEqual(new[]{1120,1106,1066,1126}));
        Render(this,Path.Combine(directory,"collection-en.png"));
        _preferences.Save(Path.Combine(directory,"preferences.json"));
        var restored=AppPreferences.Load(Path.Combine(directory,"preferences.json"));
        Check("settings_round_trip",restored.Language=="en" && restored.HudTargets.SequenceEqual(_preferences.HudTargets));
        _session.Save(Path.Combine(directory,"saved-collection.json"));
        Check("collection_saved",File.Exists(Path.Combine(directory,"saved-collection.json")));
        var handle=new WindowInteropHelper(this).Handle;
        using(var keys=new HudHotkeys(handle))
        {
            var delivered=false;
            var registered=keys.Register(0xC291,0x76,()=>delivered=true);
            OverlayNative.SendMessageW(handle,0x0312,0xC291,0);
            Check("native_hotkey_dispatch",registered && delivered);
        }
        CreateTray();
        Check("tray_icon_created",_tray?.Available==true);
        Close();
        Check("close_hides_to_tray",!IsVisible && _tray?.Available==true && _hud.IsVisible && _map.IsVisible);
        StartupWindow.ShowVisible(this,false);
        Check("tray_window_restored",IsVisible);
        _tray?.Dispose();_tray=null;
        Hide();Check("startup_restores_window",StartupWindow.ShowVisible(this,false) && StartupWindow.IsWindowVisible(handle));
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
        if(checks.Any(x=>!x.Value))throw new InvalidOperationException("Failed: "+string.Join(", ",checks.Where(x=>!x.Value).Select(x=>x.Key)));
    }
    private static T? FindVisual<T>(DependencyObject item) where T:DependencyObject
    {
        if(item is T match)return match;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(item);i++)
            if(FindVisual<T>(VisualTreeHelper.GetChild(item,i)) is T found)return found;
        return null;
    }
    private static void Render(Window window,string path)
    {
        var content=(FrameworkElement)window.Content;content.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth+content.Margin.Left+content.Margin.Right),
            (int)Math.Ceiling(content.ActualHeight+content.Margin.Top+content.Margin.Bottom),96,96,PixelFormats.Pbgra32);
        bitmap.Render(content);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(path);png.Save(file);
    }
}
