using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using SoulTracker.Core;
namespace SoulTracker.App;

public partial class MainWindow
{
    private void VerifyDesktopControls(string directory,Dictionary<string,bool> checks,CollectionView sample)
    {
        void Check(string name,bool value)=>checks.Add(name,value);
        SelectPage(true);UpdateLayout();
        Check("settings_navigation_and_language",SettingsPage.IsVisible && !CollectionPage.IsVisible && LanguagePicker.IsVisible);
        Check("default_hotkey_fields",HudKeyInput.Text=="Ctrl+Alt+H" && MapKeyInput.Text=="Ctrl+Alt+M" && LockKeyInput.Text=="Ctrl+Alt+L");
        var badge=FindVisual<HelpBadge>(SettingsPage)!;
        Check("help_translated",badge.ToolTip is ToolTip {Content:TextBlock text} && text.Text.Length>30);
        Render(this,Path.Combine(directory,"settings-ru.png"));
        HudKeyInput.Text="Ctrl+Shift+F9";MapKeyInput.Text="Ctrl+Shift+F9";
        ApplyHotkeys(this,new RoutedEventArgs());
        Check("duplicate_hotkeys_rejected",_preferences.HudHotkey=="Ctrl+Alt+H" && HotkeyStatus.Text.Contains("разные"));
        MapKeyInput.Text="Ctrl+Shift+F10";LockKeyInput.Text="Ctrl+Shift+F11";
        ApplyHotkeys(this,new RoutedEventArgs());
        Check("custom_hotkeys_applied",_preferences.HudHotkey=="Ctrl+Shift+F9" && _hud.HotkeyHint.Text.Contains("Ctrl+Shift+F9") && _map.HideButton.ToolTip.ToString()!.Contains("Ctrl+Shift+F10"));
        Check("hotkey_parser_validates",HotkeySpec.TryParse("shift+control+9",out var key) && key!.Text=="Ctrl+Shift+9" &&
            !HotkeySpec.TryParse("Ctrl+Ctrl+H",out _) && !HotkeySpec.TryParse("A",out _) && HotkeySpec.TryParse("F24",out _));
        var width=_hud.Width;var height=_hud.Height;
        HudRowScale.Value=.75;
        Check("hud_row_scale_independent",_hud.RowScaleTransform.ScaleX==.75 && _hud.Width==width && _hud.Height==height);
        Render(_hud,Path.Combine(directory,"hud-small.png"));
        HudRowScale.Value=1.5;Render(_hud,Path.Combine(directory,"hud-large.png"));
        HudRowScale.Value=1;
        SelectPage(false);
        Check("collection_navigation",CollectionPage.IsVisible && !SettingsPage.IsVisible && !LanguagePicker.IsVisible);
        var quick=BuildHudQuickSettings();
        MenuItem Quick(string name)=>quick.Items.OfType<MenuItem>().Single(x=>Equals(x.Tag,name));
        var autoBefore=_preferences.AutoTrackSouls;
        Quick("AutoTrackSouls").IsChecked=!autoBefore;
        Check("hud_menu_changes_auto_tracking",_preferences.AutoTrackSouls==!autoBefore);
        Quick("AutoTrackSouls").IsChecked=autoBefore;
        var feraBefore=Fera.IsChecked==true;
        Quick("Fera").IsChecked=!feraBefore;
        Check("hud_menu_changes_categories",_preferences.Categories.Contains("Fera")==!feraBefore);
        Quick("Fera").IsChecked=feraBefore;
        var mapBefore=_preferences.MapVisible;
        Quick("MapVisible").IsChecked=!mapBefore;
        Check("hud_menu_changes_map",_preferences.MapVisible==!mapBefore);
        Quick("MapVisible").IsChecked=mapBefore;
        _preferences.Save(Path.Combine(directory,"hud-menu-preferences.json"));
        var menuSaved=AppPreferences.Load(Path.Combine(directory,"hud-menu-preferences.json"));
        Check("hud_menu_preferences_saved",menuSaved.Categories.SequenceEqual(_preferences.Categories));
        _hud.QuickSettingsButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var visibleMenu=_hud.QuickSettingsButton.ContextMenu!;
        visibleMenu.UpdateLayout();
        bool Readable(ItemsControl parent)=>parent.Items.OfType<MenuItem>().All(item=>
            item.Header is TextBlock {Foreground:System.Windows.Media.SolidColorBrush brush} &&
            brush.Color==System.Windows.Media.Colors.Black && Readable(item));
        Check("hud_menu_headers_readable",visibleMenu.IsOpen && Readable(visibleMenu));
        var helpItem=visibleMenu.Items.OfType<MenuItem>().First(x=>x.ToolTip is ToolTip);
        var helpTip=(ToolTip)helpItem.ToolTip;
        Check("hud_menu_help_wraps",helpTip.Content is TextBlock {TextWrapping:TextWrapping.Wrap,MaxWidth:300} && !helpTip.IsHitTestVisible);
        helpTip.IsOpen=true;
        helpTip.UpdateLayout();RenderElement(helpTip,Path.Combine(directory,"hud-help-wrapped.png"));
        helpItem.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=MouseLeaveEvent});
        Check("hud_menu_help_closes_on_leave",!helpTip.IsOpen);
        RenderElement(visibleMenu,Path.Combine(directory,"hud-menu.png"));
        var submenu=visibleMenu.Items.OfType<MenuItem>().Single(x=>x.HasItems);
        submenu.IsSubmenuOpen=true;submenu.UpdateLayout();
        var popup=submenu.Template.FindName("PART_Popup",submenu) as Popup;
        Check("hud_submenu_opens",popup?.IsOpen==true);
        if(popup?.Child is FrameworkElement child)RenderElement(child,Path.Combine(directory,"hud-submenu.png"));
        submenu.IsSubmenuOpen=false;helpTip.IsOpen=true;visibleMenu.IsOpen=false;
        Check("hud_menu_help_closes_with_menu",!helpTip.IsOpen);
        _map.SettingsButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check("map_settings_can_expand",_map.TargetSettings.Visibility==Visibility.Visible);
        Render(_map,Path.Combine(directory,"map-expanded.png"));
        _map.SettingsButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        _preferences.Save(Path.Combine(directory,"desktop-preferences.json"));
        var loaded=AppPreferences.Load(Path.Combine(directory,"desktop-preferences.json"));
        Check("custom_preferences_round_trip",loaded.HudHotkey=="Ctrl+Shift+F9" && loaded.MapHotkey=="Ctrl+Shift+F10" && loaded.HudRowScale==1);
        ResetHotkeys(this,new RoutedEventArgs());ApplyHotkeys(this,new RoutedEventArgs());
        var handle=new WindowInteropHelper(this).Handle;var other=new WindowInteropHelper(_hud).Handle;
        using var blocker=new HudHotkeys(other);using var bindings=new HudHotkeys(handle);
        HotkeySpec.TryParse("Ctrl+Shift+Alt+F23",out var old);HotkeySpec.TryParse("Ctrl+Shift+Alt+F24",out var conflict);
        var invoked=false;
        var oldOk=bindings.Replace([new(0xC295,old!,()=>invoked=true)],out _,out _);
        var blocked=blocker.Replace([new(0xC296,conflict!,()=>{})],out _,out _);
        var rejected=!bindings.Replace([new(0xC295,conflict!,()=>{})],out _,out var rollback);
        OverlayNative.SendMessageW(handle,0x0312,0xC295,0);
        Check("native_conflict_restores_previous_binding",oldOk && blocked && rejected && rollback && invoked);
    }
}
