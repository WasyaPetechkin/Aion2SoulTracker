using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace SoulTracker.App;

public partial class MainWindow
{
    private bool _settingsVisible;
    private void ShowCollectionPage(object sender,RoutedEventArgs e)=>SelectPage(false);
    private void ShowSettingsPage(object sender,RoutedEventArgs e)=>SelectPage(true);
    private void SelectPage(bool settings)
    {
        _settingsVisible=settings;
        CollectionPage.Visibility=settings ? Visibility.Collapsed : Visibility.Visible;
        SettingsPage.Visibility=settings ? Visibility.Visible : Visibility.Collapsed;
        CollectionNav.Background=new SolidColorBrush(settings ? Color.FromRgb(27,42,60) : Color.FromRgb(43,89,92));
        SettingsNav.Background=new SolidColorBrush(settings ? Color.FromRgb(43,89,92) : Color.FromRgb(27,42,60));
    }
    private void TranslateSettings()
    {
        var t=Text;
        CollectionNav.Content=t.Choose("Коллекция","Collection");SettingsNav.Content=t.Choose("Настройки","Settings");
        ExitButton.Content=t.Choose("⏻ Выход","⏻ Exit");SettingsTitle.Text=t.Choose("Настройки программы","App settings");
        SettingsSubtitle.Text=t.Choose("Интерфейс и управление","Interface and controls");
        HotkeysTitle.Text=t.Choose("Горячие клавиши","Hotkeys");
        HotkeysHelp.Text=t.Choose("Нажми на поле и введи сочетание клавиш. Затем нажми «Применить». Прежние назначения работают до сохранения.",
            "Click a field and press a shortcut, then click Apply. Previous bindings remain active until saved.");
        HudKeyLabel.Text=t.Choose("Счётчик душ","Soul counter");MapKeyLabel.Text=t.Choose("Интерактивная карта","Interactive map");
        LockKeyLabel.Text=t.Choose("Клики в игру","Click through");
        ApplyKeysButton.Content=t.Choose("Применить","Apply");ResetKeysButton.Content=t.Choose("По умолчанию","Defaults");
        ScaleTitle.Text=t.Choose("Размер строк счётчика","Counter row size");
        ScaleHelp.Text=t.Choose("Меняет бары, имена и числа. Размер окна HUD остаётся прежним.","Scales bars, names and numbers. The HUD window keeps its size.");
        ScaleValue.Text=$"{_preferences.HudRowScale:P0}";
        FocusMode.Content=t.Choose("Режим фокуса","Focus mode");
        SelectPage(_settingsVisible);HelpBadge.TranslateTree(this,_language);
        _tray?.Configure(_language,_preferences.HudVisible,_preferences.MapVisible);
    }
    private void LoadHotkeyInputs()
    {HudKeyInput.Text=_preferences.HudHotkey;MapKeyInput.Text=_preferences.MapHotkey;LockKeyInput.Text=_preferences.LockHotkey;}
    private void CaptureHotkey(object sender,KeyEventArgs e)
    {
        if(sender is not TextBox input)return;
        var key=e.Key==Key.System ? e.SystemKey : e.Key;
        if(key==Key.Tab && Keyboard.Modifiers==ModifierKeys.None)return;
        e.Handled=true;
        if(key==Key.Escape){LoadHotkeyInputs();HotkeyStatus.Text="";return;}
        if(HotkeySpec.FromKey(key,HotkeySpec.CurrentModifiers(),out var spec))
        {input.Text=spec!.Text;HotkeyStatus.Text=Text.Choose("Нажми «Применить», чтобы сохранить.","Click Apply to save.");}
    }
    private void ResetHotkeys(object sender,RoutedEventArgs e)
    {HudKeyInput.Text="Ctrl+Alt+H";MapKeyInput.Text="Ctrl+Alt+M";LockKeyInput.Text="Ctrl+Alt+L";HotkeyStatus.Text=Text.Choose("Нажми «Применить», чтобы сохранить.","Click Apply to save.");}
    private void ApplyHotkeys(object sender,RoutedEventArgs e)
    {
        if(!HotkeySpec.TryParse(HudKeyInput.Text,out var hud) || !HotkeySpec.TryParse(MapKeyInput.Text,out var map) || !HotkeySpec.TryParse(LockKeyInput.Text,out var locked))
        {HotkeyStatus.Text=Text.Choose("Введи сочетание с модификатором или клавишу F1–F24.","Use a modifier shortcut or F1–F24.");return;}
        if(new[]{hud!.Text,map!.Text,locked!.Text}.Distinct().Count()!=3)
        {HotkeyStatus.Text=Text.Choose("Назначь разные сочетания для трёх действий.","Use a different shortcut for each action.");return;}
        if(_hotkeys is not null && !_hotkeys.Replace(Bindings(hud,map,locked),out var error,out var restored))
        {
            HotkeyStatus.Text=Text.Choose($"Не удалось назначить {error}: сочетание занято или недоступно в Windows.",$"Cannot bind {error}: the shortcut is in use or unavailable in Windows.")+
                (restored ? Text.Choose(" Прежние назначения сохранены."," Previous bindings were restored.") : Text.Choose(" Не все прежние клавиши удалось восстановить; назначь новые."," Some previous bindings could not be restored; choose new ones."));
            _hotkeyUnavailable=!restored;return;
        }
        _preferences.HudHotkey=hud.Text;_preferences.MapHotkey=map.Text;_preferences.LockHotkey=locked.Text;
        _hotkeyUnavailable=false;var saved=SavePreferences();ApplyLanguage();
        HotkeyStatus.Text=saved ? Text.Choose("Горячие клавиши сохранены.","Hotkeys saved.") :
            Text.Choose("Клавиши работают до закрытия программы. ","Shortcuts are active for this session. ")+_notice;
    }
    private HudHotkeys.Binding[] Bindings(HotkeySpec hud,HotkeySpec map,HotkeySpec locked)=>
        [new(0xC281,hud,()=>DispatchShortcut(hud,()=>SetHudVisibility(!_preferences.HudVisible))),
         new(0xC283,map,()=>DispatchShortcut(map,()=>SetMapVisibility(!_preferences.MapVisible))),
         new(0xC282,locked,()=>DispatchShortcut(locked,()=>SetHudLock(!_preferences.HudLocked)))];
    private void DispatchShortcut(HotkeySpec shortcut,Action action)
    {
        if(Keyboard.FocusedElement is TextBox input && (input==HudKeyInput || input==MapKeyInput || input==LockKeyInput))
        {input.Text=shortcut.Text;HotkeyStatus.Text=Text.Choose("Нажми «Применить», чтобы сохранить.","Click Apply to save.");}
        else action();
    }
    private void RegisterSavedHotkeys()
    {
        HotkeySpec.TryParse(_preferences.HudHotkey,out var hud);HotkeySpec.TryParse(_preferences.MapHotkey,out var map);HotkeySpec.TryParse(_preferences.LockHotkey,out var locked);
        _hotkeyUnavailable=!_hotkeys!.Replace(Bindings(hud!,map!,locked!),out var error,out _);
        if(_hotkeyUnavailable)HotkeyStatus.Text=Text.Choose($"Сочетание {error} занято. Выбери другие клавиши и нажми «Применить».",$"Shortcut {error} is unavailable. Choose other keys and click Apply.");
        ApplyLanguage();
    }
    private void RowScaleChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(_session is null)return;
        _preferences.HudRowScale=HudRowScale.Value;ScaleValue.Text=$"{_preferences.HudRowScale:P0}";
        _hud.Configure(_preferences,_language);QueueSettingsSave();
    }
    private void FocusChanged(object sender,RoutedEventArgs e)
    {
        if(_session is null)return;
        _preferences.FocusMode=FocusMode.IsChecked==true;
        _hud.Configure(_preferences,_language);_map.Configure(_language);Refresh(true);QueueSettingsSave();
    }
    private void ExitClicked(object sender,RoutedEventArgs e)=>ExitApplication();
    internal void PrepareExit()=>_exiting=true;
    private void ExitApplication(){PrepareExit();Close();}
    private void CreateTray()
    {
        if(_tray is not null)return;
        try
        {
            _tray=new(()=>StartupWindow.ShowVisible(this),()=>SetHudVisibility(!_preferences.HudVisible),
                ()=>SetMapVisibility(!_preferences.MapVisible),ExitApplication);
            _tray.Configure(_language,_preferences.HudVisible,_preferences.MapVisible);
        }
        catch(Exception ex){_tray?.Dispose();_tray=null;_notice=Text.Choose("Не удалось создать иконку трея: ","Could not create tray icon: ")+ex.Message;}
    }
    internal bool HideToTray()
    {
        if(_tray?.Available!=true)return false;
        SaveCollection();SavePreferences();Hide();_tray.ExplainOnce(_language);return true;
    }
}
