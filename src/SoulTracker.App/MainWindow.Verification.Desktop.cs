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
        var manualTargets=_preferences.HudTargets.ToArray();var manualMapTarget=_preferences.MapPetId;
        // Known mapped pets; all others complete so leadership and map counts are deterministic.
        var tied=sample with {LastTimeMs=sample.LastTimeMs+10, Pets=sample.Pets.Select(p=>p with {Level=3, Souls=0})
            .Select(p=>p.Id is 1120 or 1106 ? p with {Level=2, Souls=70} : p).ToArray()};
        _session!.State.Restore(tied);FocusMode.IsChecked=true;Refresh(true);
        int Expected(CollectionView v)=>_spawns.Select(_preferences.MapWorld,v,HudProjection.Focus(v,EnabledCategories()).Visible.Select(p=>p.Id),false,EnabledCategories(),false).Count;
        Check("focus_ties_in_counter_and_map",_hud.Targets.Items.Count==2 && _map.SpawnCount==Expected(_session.State.View()) && _map.SpawnCount>0 && !_map.AllIncomplete.IsEnabled && !_map.TargetPicker.IsEnabled);
        Render(_hud,Path.Combine(directory,"focus-tie.png"));
        var leader=tied with {LastTimeMs=tied.LastTimeMs+1,Pets=tied.Pets.Select(p=>p.Id==1120 ? p with {Souls=71} : p).ToArray()};
        _session.State.Restore(leader);Refresh(true);
        Check("focus_narrows_after_gain",_hud.Targets.Items.Count==1 && _map.SpawnCount==Expected(_session.State.View()));
        Render(_map,Path.Combine(directory,"map-focus.png"));
        var next=leader with {LastTimeMs=leader.LastTimeMs+1,Pets=leader.Pets.Select(p=>p.Id==1120 ? p with {Level=3, Souls=0} : p).ToArray()};
        _session.State.Restore(next);Refresh(true);
        Check("focus_advances_after_max",_hud.Targets.Items.Count==1 && _map.SpawnCount==Expected(_session.State.View()) &&
            ((HudWindow.TargetRow)_hud.Targets.Items[0]).Name==PetText.Name(next.Pets.Single(p=>p.Id==1106),_language));
        var done=next with {LastTimeMs=next.LastTimeMs+1,Pets=next.Pets.Select(p=>p with {Level=3, Souls=0}).ToArray()};
        _session.State.Restore(done);Refresh(true);
        Check("focus_empty_no_category_fallback",_hud.Targets.Items.Count==0 && _map.SpawnCount==0);
        _session.State.Restore(sample with {LastTimeMs=done.LastTimeMs+1});Refresh(true);FocusMode.IsChecked=false;Refresh(true);
        Check("focus_preserves_manual_choices",_preferences.HudTargets.SequenceEqual(manualTargets) && _preferences.MapPetId==manualMapTarget);
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
