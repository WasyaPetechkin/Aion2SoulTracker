using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using SoulTracker.Core;
namespace SoulTracker.App;
public partial class MainWindow
{
    private void AutoTrackChanged(object sender,RoutedEventArgs e)
    {
        if(_session is null)return;
        _session.DrainReceivedSouls();_preferences.AutoTrackSouls=AutoTrackSouls.IsChecked==true;QueueSettingsSave();
    }
    private string _catalogSort="RemainingSort";
    private ListSortDirection _catalogDirection=ListSortDirection.Ascending;
    private void SortCatalog(object sender,DataGridSortingEventArgs e)
    {
        e.Handled=true;
        _catalogDirection=_catalogSort==e.Column.SortMemberPath && _catalogDirection==ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        _catalogSort=e.Column.SortMemberPath;
        foreach(var column in Pets.Columns)column.SortDirection=null;
        e.Column.SortDirection=_catalogDirection;Refresh(true);
    }
    private object SortValue(PetRow row)=>_catalogSort switch
    {"Name"=>row.Name,"Genus"=>row.Genus,"Id"=>row.Id,"LevelSort"=>row.LevelSort,"SoulsSort"=>row.SoulsSort,_=>row.RemainingSort};
    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if(_session is null)return;
        if(sender==Cogni || sender==Fera || sender==Natura || sender==Varian)
        {_preferences.Categories=EnabledCategories();QueueSettingsSave();}
        Refresh(true);
    }
    private void LanguageChanged(object sender,SelectionChangedEventArgs e)
    {
        if(_session is null || LanguagePicker.SelectedItem is not ComboBoxItem item) return;
        _language=item.Tag?.ToString()=="en" ? "en" : "ru";
        _preferences.Language=_language;
        QueueSettingsSave();
        ApplyLanguage();
        Refresh(true);
    }
    private void ApplyLanguage()
    {
        var t=Text;
        Title="Aion 2 · "+t.Title;
        Heading.Text=t.Title;
        LanguageLabel.Text=t.Choose("Язык","Language");
        ExportButton.Content=t.Choose("Сохранённые данные","Saved data");
        Cogni.Content=PetText.Category("Cogni",_language);
        Fera.Content=PetText.Category("Fera",_language);
        Natura.Content=PetText.Category("Natura",_language);
        Varian.Content=PetText.Category("Varian",_language);
        Special.Content=PetText.Category("Special",_language);
        CategoryHint.Text=t.Choose("Категории фильтруют список и карту. Если цели HUD не выбраны, карта показывает всех незавершённых из этих категорий.",
            "Categories filter the list and map. With no HUD targets selected, the map shows all incomplete targets in these categories.");
        HideCompleted.Content=t.Choose("Скрыть MAX","Hide MAX");
        OwnedOnly.Content=t.Choose("Только полученные","Owned only");
        HudVisible.Content=t.Choose("Прогресс целей","Target progress");
        AutoTrackSouls.Content=t.Choose("Добавлять в HUD при получении души","Add to HUD when a soul is received");
        MapVisible.Content=t.Choose("Отдельная карта спавнов","Separate spawn map");
        MapVisible.ToolTip=t.Choose("Компактная карта. Территория и фильтры — под кнопкой ⋯ в её заголовке.","Compact map. Territory and filters are under ⋯ in its header.");
        HudLocked.Content=t.Choose("Клики в игру","Click through");
        HudOpacityLabel.Text=t.Choose("Плотность фона","Background opacity");
        HudHint.Text=_hotkeyUnavailable ? t.Choose("Одна из горячих клавиш занята другим приложением. Управляй HUD здесь.","A hotkey is in use by another app. Use the HUD controls here.") :
            t.Choose($"Цели — в колонке HUD · {_preferences.HudHotkey} прогресс · {_preferences.MapHotkey} карта · {_preferences.LockHotkey} клики в игру",
                $"Targets in the HUD column · {_preferences.HudHotkey} progress · {_preferences.MapHotkey} map · {_preferences.LockHotkey} click through");
        Search.ToolTip=t.Choose("Поиск по русскому или английскому имени, либо ID","Search by Russian or English name, or ID");
        var pinHelp=new HelpBadge {Topic="pin",Margin=new Thickness(5,0,0,0)};pinHelp.Translate(_language);
        var pinHeader=new StackPanel {Orientation=Orientation.Horizontal};
        pinHeader.Children.Add(new TextBlock {Text="HUD"});pinHeader.Children.Add(pinHelp);Pets.Columns[0].Header=pinHeader;
        var headers=new[]{t.Choose("Монстр / питомец","Monster / pet"),t.Choose("Категория","Genus"),
            t.Choose("Уровень","Level"),t.Choose("Души на ступени","Current step souls"),t.Choose("До максимума","Souls to maximum"),"ID"};
        for(var i=0;i<headers.Length;i++) Pets.Columns[i+1].Header=headers[i];
        _hud.Configure(_preferences,_language);
        _map.Configure(_language);
        TranslateSettings();
        Footnote.Text=t.Choose(
            "У обычных питомцев уровни повышаются автоматически: 5 → 25 → 75 душ. MAX скрывается фильтром «Скрыть MAX». После пропуска данных потребуется новый вход в персонажа.",
            "Regular pets advance automatically at 5 → 25 → 75 souls. Hide MAX removes completed pets. Log in again after missed data to sync the collection.");
    }
    private void Refresh(bool force = false)
    {
        if (_session is null) return;
        var v = _session.State.View();
        var gains=_session.DrainReceivedSouls();
        if(_preferences.AutoTrackSouls && gains.Count>0)
        {
            var pins=_preferences.HudTargets.ToHashSet();
            foreach(var gain in gains)
                if(gain.WorldId is int world && _spawns.Worlds.ContainsKey(world) && v.Pets.Any(p=>p.Id==gain.PetId && p.IsRegular && !p.Completed))pins.Add(gain.PetId);
            if(pins.Count!=_preferences.HudTargets.Length){_preferences.HudTargets=pins.Order().ToArray();QueueSettingsSave();force=true;}
        }
        _hud.Refresh(v,_preferences,EnabledCategories(),_spawns,_session.Navigation.View().WorldId);
        _map.Refresh(v,_session.Navigation.View(),EnabledCategories());
        var t=Text;
        Summary.Text = t.Summary(v);
        Character.Text = (v.Character.Length > 0 ? v.Character + "  ·  " : "") + t.Subtitle;
        Source.Text = t.Source(v.Source) + (v.NeedsResync ? t.Choose("  ·  нужен новый вход в персонажа","  ·  log in again to sync") : "") +
            (v.LastTimeMs > 0 ? $"  ·  {DateTimeOffset.FromUnixTimeMilliseconds(v.LastTimeMs).ToLocalTime():HH:mm:ss}" : "");
        CaptureStatus.Text = _notice.Length > 0 ? _notice : t.Status(_session.Status);
        UpdatePrerequisite();
        Recent.Text = t.History(v.History.LastOrDefault() ?? "",v.Pets);
        var sig = $"{v.LastTimeMs}:{v.HasSnapshot}:{v.NeedsResync}:{v.Pets.Count}:{_language}:{string.Join(',',_preferences.HudTargets)}";
        if (force || sig != _signature)
        {
            _signature = sig;
            var enabled = EnabledCategories().ToHashSet();
            if (Special.IsChecked==true) { enabled.Add("Special"); enabled.Add("Unknown"); }
            var text = Search.Text.Trim();
            var selectedId=(Pets.SelectedItem as PetRow)?.Id;
            var rows = v.HasSnapshot ? v.Pets.Where(x => _preferences.HudTargets.Contains(x.Id) ||
                    (enabled.Contains(x.Genus) && (HideCompleted.IsChecked!=true || !x.Completed) &&
                    (OwnedOnly.IsChecked!=true || x.Level>0) && PetText.Matches(x,text)))
                .Select(x=>new PetRow(x.Id,PetText.Name(x,_language),PetText.Category(x.Genus,_language),
                    PetText.Level(x,_language),x.Stage,PetText.Remaining(x,_language),_preferences.HudTargets.Contains(x.Id),x.IsRegular,x.Level,x.Souls,x.Remaining ?? int.MaxValue)).ToArray() : Array.Empty<PetRow>();
            var pinnedFirst=rows.OrderByDescending(x=>x.IsTracked);
            Pets.ItemsSource=(_catalogDirection==ListSortDirection.Ascending ? pinnedFirst.ThenBy(SortValue) : pinnedFirst.ThenByDescending(SortValue)).ThenBy(x=>x.Name).ToArray();
            foreach(var column in Pets.Columns)column.SortDirection=column.SortMemberPath==_catalogSort ? _catalogDirection : null;
            if(selectedId.HasValue) Pets.SelectedItem=rows.FirstOrDefault(x=>x.Id==selectedId);
        }
        if (++_ticks % 5 == 0)
        {
            SaveCollection();
            try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(_export)!,"status.json"),
                JsonSerializer.Serialize(new { Time=DateTimeOffset.UtcNow, _session.Status,
                    _session.LiveSnapshotReceived, _session.RejectedPetMessages, v.HasSnapshot, v.NeedsResync,
                    HudVisible=_hud.IsVisible,HudLocked=_preferences.HudLocked,HudTargets=_preferences.HudTargets,
                    MapVisible=_map.IsVisible,MapWorld=_preferences.MapWorld,MapSpawns=_map.SpawnCount,Navigation=_session.Navigation.View(),
                    CaptureHealth=_session.Health,MapPositionIntervalMs=_navigationTimer.Interval.TotalMilliseconds,
                    HotkeysAvailable=!_hotkeyUnavailable })); }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { _notice=ex.Message; }
        }
    }
    private void OpenExport(object sender,RoutedEventArgs e)
    {
        SaveCollection();
        OpenPath(Path.GetDirectoryName(_export)!);
    }
    private void PinClicked(object sender,RoutedEventArgs e)
    {
        if(sender is not CheckBox {DataContext:PetRow pet} box) return;
        var targets=_preferences.HudTargets.ToHashSet();
        if(box.IsChecked==true) targets.Add(pet.Id); else targets.Remove(pet.Id);
        _preferences.HudTargets=targets.Order().ToArray();
        QueueSettingsSave();
        Refresh(true);
    }
    private void HudVisibilityChanged(object sender,RoutedEventArgs e)
    {
        if(_session is null || _changingHudControls) return;
        SetHudVisibility(HudVisible.IsChecked==true);
    }
    private string[] EnabledCategories()=>new[]{
        ("Cogni",Cogni.IsChecked==true),("Fera",Fera.IsChecked==true),
        ("Natura",Natura.IsChecked==true),("Varian",Varian.IsChecked==true)}.Where(x=>x.Item2).Select(x=>x.Item1).ToArray();
    private void MapVisibilityChanged(object sender,RoutedEventArgs e)
    {
        if(_session is null || _changingHudControls)return;
        SetMapVisibility(MapVisible.IsChecked==true);
    }
    private void SetMapVisibility(bool visible)
    {
        _preferences.MapVisible=visible;
        _tray?.Configure(_language,_preferences.HudVisible,visible);
        _changingHudControls=true;MapVisible.IsChecked=visible;_changingHudControls=false;
        UpdateHudWindow();QueueSettingsSave();
    }
    private void HudLockChanged(object sender,RoutedEventArgs e)
    {
        if(_session is null || _changingHudControls) return;
        SetHudLock(HudLocked.IsChecked==true);
    }
    private void SetHudVisibility(bool visible)
    {
        _preferences.HudVisible=visible;
        _tray?.Configure(_language,visible,_preferences.MapVisible);
        _changingHudControls=true;
        HudVisible.IsChecked=visible;
        _changingHudControls=false;
        UpdateHudWindow(); QueueSettingsSave();
    }
    private void SetHudLock(bool locked)
    {
        _preferences.HudLocked=locked;
        _changingHudControls=true;
        HudLocked.IsChecked=locked;
        _changingHudControls=false;
        _hud.Configure(_preferences,_language);
        _map.Configure(_language);
        QueueSettingsSave();
    }
    private void HudAppearanceChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(_session is null) return;
        _preferences.HudOpacity=HudOpacity.Value;
        _hud.Configure(_preferences,_language);
        _map.Configure(_language);
        QueueSettingsSave();
    }
    private void UpdateHudWindow()
    {
        if(!_loaded || _previewMode) return;
        if(_preferences.HudVisible) { _hud.Configure(_preferences,_language); _hud.Show(); }
        else _hud.Hide();
        if(_preferences.MapVisible){_map.Configure(_language);_map.Show();}
        else _map.Hide();
    }
    private void QueueSettingsSave()
    {
        if(_previewMode) return;
        _saveSettingsTimer.Stop(); _saveSettingsTimer.Start();
    }
    private bool SavePreferences()
    {
        if(_previewMode) return true;
        try {_preferences.Save(_settingsPath);return true;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException)
        { _notice=Text.Choose("Не удалось сохранить настройки: ","Could not save preferences: ")+e.Message;return false; }
    }
}
