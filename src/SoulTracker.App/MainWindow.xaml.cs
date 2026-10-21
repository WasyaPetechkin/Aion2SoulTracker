using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AionMeter.Core.Capture;
using AionMeter.Core.Game;
using SoulTracker.Core;
namespace SoulTracker.App;
public partial class MainWindow : Window
{
    private TrackerSession? _session;
    private readonly DispatcherTimer _timer=new() {Interval=TimeSpan.FromSeconds(1)};
    private readonly DispatcherTimer _navigationTimer=new() {Interval=TimeSpan.FromMilliseconds(100)};
    private readonly DispatcherTimer _saveSettingsTimer=new() {Interval=TimeSpan.FromMilliseconds(450)};
    private readonly string _export;
    private readonly string _settingsPath;
    private readonly AppPreferences _preferences;
    private readonly HudWindow _hud;
    private readonly MapWindow _map;
    private readonly SpawnDatabase _spawns;
    private readonly bool _previewMode;
    private readonly bool _offline;
    private bool _captureStarted;
    private bool _loaded;
    private bool _changingHudControls;
    private bool _hotkeyUnavailable;
    private HudHotkeys? _hotkeys;
    private TrayController? _tray;
    private bool _exiting;
    private string _signature="";
    private string _notice="";
    private string _language="ru";
    private int _ticks;
    private UiText Text=>new(_language);
    public sealed record PetRow(int Id,string Name,string Genus,string LevelText,string Stage,string RemainingText,bool IsTracked,bool CanTrack,int LevelSort,int SoulsSort,int RemainingSort);

    public MainWindow(string[] args)
    {
        _previewMode=args.Contains("--verify");_offline=args.Contains("--offline") || _previewMode;
        _settingsPath=Path.Combine(App.DataDirectory,"settings.json");
        _export=Path.Combine(App.DataDirectory,"collection.json");
        _preferences=AppPreferences.Load(_settingsPath);_language=_preferences.Language;
        InitializeComponent();
        Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/app.ico"));
        LanguagePicker.SelectedIndex=_language=="ru" ? 0 : 1;
        HudVisible.IsChecked=_preferences.HudVisible;MapVisible.IsChecked=_preferences.MapVisible;
        HudLocked.IsChecked=_preferences.HudLocked;HudOpacity.Value=_preferences.HudOpacity;
        FocusMode.IsChecked=_preferences.FocusMode;HudRowScale.Value=_preferences.HudRowScale;LoadHotkeyInputs();
        AutoTrackSouls.IsChecked=_preferences.AutoTrackSouls;
        Cogni.IsChecked=_preferences.Categories.Contains("Cogni");Fera.IsChecked=_preferences.Categories.Contains("Fera");
        Natura.IsChecked=_preferences.Categories.Contains("Natura");Varian.IsChecked=_preferences.Categories.Contains("Varian");
        Width=Math.Max(MinWidth,Math.Min(Width,SystemParameters.WorkArea.Width-24));
        Height=Math.Max(MinHeight,Math.Min(Height,SystemParameters.WorkArea.Height-24));
        _hud=new(_preferences,_previewMode);
        _spawns=SpawnDatabase.Load(Path.Combine(AppContext.BaseDirectory,"maps","spawns.json"));
        _map=new(_spawns,_preferences,_previewMode);
        _map.PreferencesChanged+=QueueSettingsSave;
        _map.HideRequested+=()=>SetMapVisibility(false);
        _map.LockRequested+=()=>SetHudLock(!_preferences.HudLocked);
        _hud.CatalogRequested+=()=>StartupWindow.ShowVisible(this);
        _hud.QuickSettingsRequested=BuildHudQuickSettings;
        _hud.HideRequested+=()=>SetHudVisibility(false);
        _hud.LockRequested+=()=>SetHudLock(!_preferences.HudLocked);
        _hud.UntrackRequested+=id=>{_preferences.HudTargets=_preferences.HudTargets.Where(x=>x!=id).ToArray();QueueSettingsSave();Refresh(true);};
        _hud.GeometryChanged+=()=>
        {
            _preferences.HudLeft=_hud.Left;_preferences.HudTop=_hud.Top;
            _preferences.HudWidth=_hud.Width;_preferences.HudHeight=_hud.Height;QueueSettingsSave();
        };
        var catalog=JsonSerializer.Deserialize<List<PetDefinition>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"pet-catalog.json")))!;
        _session=new(catalog,GameData.Load(Path.Combine(AppContext.BaseDirectory,"data"),"en"));
        _session.Restore(_export);
        ApplyLanguage();
        if(!_offline) StartCapture();
        else _notice=Text.Choose("Просмотр сохранённых данных · чтение игры выключено","Viewing saved data · game capture is off");
        _saveSettingsTimer.Tick+=(_,_)=>{_saveSettingsTimer.Stop();SavePreferences();};
        SourceInitialized+=(_,_)=>
        {
            if(_previewMode)return;
            _hotkeys=new(new WindowInteropHelper(this).Handle);
            RegisterSavedHotkeys();
        };
        Loaded+=(_,_)=>{_loaded=true;if(!_previewMode)CreateTray();UpdateHudWindow();};
        _timer.Tick+=(_,_)=>Refresh();
        _navigationTimer.Tick+=(_,_)=>{if(_map.IsVisible)_map.RefreshNavigation(_session.Navigation.View());};
        if(!_previewMode){_timer.Start();_navigationTimer.Start();}
        Closing+=(_,e)=>
        {
            if(!_exiting && HideToTray()){e.Cancel=true;return;}
            _timer.Stop();_navigationTimer.Stop();_saveSettingsTimer.Stop();
            if(!_previewMode){SaveCollection();SavePreferences();}
            _tray?.Dispose();_tray=null;_hotkeys?.Dispose();_hud.Close();_map.Close();_session.Dispose();
        };
        Refresh(true);
    }
    private void StartCapture()
    {
        if(_captureStarted || !LiveCapture.IsNpcapInstalled())return;
        try {_session!.StartLive();_captureStarted=true;_notice="";}
        catch(Exception ex){_notice=Text.Choose("Не удалось начать чтение игры: ","Could not start capture: ")+ex.Message;}
    }
    private void UpdatePrerequisite(bool? installed=null)
    {
        var missing=!(installed ?? LiveCapture.IsNpcapInstalled());
        PrerequisitePanel.Visibility=missing ? Visibility.Visible : Visibility.Collapsed;
        PrerequisiteText.Text=Text.Choose(
            "Для чтения игры нужен Npcap. Скачай установщик с официального сайта, установи с обычными настройками и нажми «Проверить снова». Установка драйвера может запросить права администратора.",
            "Game capture requires Npcap. Download its installer from the official site, keep the default options, then click Check again. Driver installation may require administrator permission.");
        DownloadNpcap.Content=Text.Choose("Открыть npcap.com","Open npcap.com");
        RecheckNpcap.Content=Text.Choose("Проверить снова","Check again");
        if(missing)CaptureStatus.Text=Text.Choose("Npcap не установлен · данные игры пока недоступны","Npcap is not installed · game data is unavailable");
        else if(!_offline && !_captureStarted)StartCapture();
        if(!_session!.State.View().HasSnapshot)
            Source.Text=Text.Choose("Запусти трекер, затем войди в персонажа. Если ты уже в игре — выйди к выбору персонажа и войди снова.",
                "Start the tracker, then log in to your character. If already in game, return to character selection and log in again.");
        Source.TextWrapping=TextWrapping.Wrap;
    }
    private void RetryCapture(object sender,RoutedEventArgs e){if(!_offline)StartCapture();Refresh(true);}
    private void OpenNpcap(object sender,RoutedEventArgs e)=>OpenPath("https://npcap.com/#download");
    private void OpenPath(string path)
    {
        try {Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
        catch(Exception ex){_notice=ex.Message;Refresh();}
    }
    private void SaveCollection()
    {
        if(_previewMode)return;
        try {_session?.Save(_export);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {_notice=Text.Choose("Не удалось сохранить прогресс: ","Could not save progress: ")+ex.Message;}
    }
}
