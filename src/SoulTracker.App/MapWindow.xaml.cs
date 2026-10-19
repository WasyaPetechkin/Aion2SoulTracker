using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SoulTracker.Core;

namespace SoulTracker.App;

public partial class MapWindow : Window
{
    private readonly SpawnDatabase _database;
    private readonly AppPreferences _preferences;
    private readonly bool _preview;
    private nint _handle;
    private bool _changing;
    private string _language="ru";
    private string _optionsSignature="";
    private CollectionView? _collection;
    private NavigationView _navigation=new(null,null,null,null,0,"",false);
    private string[] _categories=["Cogni","Fera","Natura","Varian"];
    internal event Action? PreferencesChanged;
    internal event Action? HideRequested;
    internal event Action? LockRequested;
    private sealed record WorldOption(int Id,string Label);
    internal int SpawnCount {get;private set;}
    internal int MarkerCount=>Map.VisibleMarkerCount;
    internal double CurrentZoom=>Map.Zoom;

    internal MapWindow(SpawnDatabase database,AppPreferences preferences,bool preview)
    {
        _database=database;_preferences=preferences;_preview=preview;
        InitializeComponent();
        Width=preferences.MapWidth;Height=preferences.MapHeight;
        Left=preferences.MapLeft ?? SystemParameters.WorkArea.Left+30;
        Top=preferences.MapTop ?? SystemParameters.WorkArea.Top+100;
        Map.SetZoom(preferences.MapZoom);
        Map.ViewChanged+=()=>{_preferences.MapZoom=Map.Zoom;PreferencesChanged?.Invoke();};
        Map.Panned+=()=>{_preferences.MapFollowPlayer=false;UpdateFollowButton();PreferencesChanged?.Invoke();};
        SourceInitialized+=(_,_)=>{_handle=new WindowInteropHelper(this).Handle;OverlayNative.SetClickThrough(_handle,_preferences.HudLocked);};
        OverlaySizing.Attach(this,ResizeCorner,()=>_preferences.HudLocked,SaveGeometry);
        Loaded+=(_,_)=>KeepOnScreen();
    }
    internal void Configure(string language)
    {
        _language=language;var t=new UiText(language);
        _changing=true;
        Width=_preferences.MapWidth;Height=_preferences.MapHeight;
        PanelBackground.Opacity=Math.Max(.65,_preferences.HudOpacity);
        Map.Locked=_preferences.HudLocked;
        ResizeCorner.Visibility=_preferences.HudLocked ? Visibility.Collapsed : Visibility.Visible;
        if(_handle!=0)OverlayNative.SetClickThrough(_handle,_preferences.HudLocked);
        MapTitle.Text=t.Choose("МЕСТА СПАВНА","SPAWN LOCATIONS");
        SettingsButton.ToolTip=t.Choose("Настройки карты: территория и цели","Map settings: territory and targets");
        ApplySettingsVisibility();
        AllIncomplete.Content=t.Choose("Все незавершённые вместо выбранных","All incomplete instead of selected");
        AllIncomplete.ToolTip=t.Choose("Показывать все незавершённые цели отмеченных категорий, даже если выбраны отдельные мобы. Без выбранных мобов это происходит автоматически.",
            "Show all incomplete targets in enabled categories even when individual targets are pinned. With no pinned targets this happens automatically.");
        AllIncomplete.IsChecked=_preferences.MapAllIncomplete;
        AllIncomplete.IsEnabled=!_preferences.FocusMode;
        LockButton.Content=_preferences.HudLocked ? "◆" : "◇";
        LockButton.ToolTip=t.Choose("Клики в игру · ","Click through · ")+_preferences.LockHotkey;
        HideButton.ToolTip=t.Choose("Скрыть карту · ","Hide map · ")+_preferences.MapHotkey;
        FitButton.ToolTip=t.Choose("Вся территория","Fit territory");
        PlayerButton.Content=t.Choose("Я","Me");
        PlayerButton.ToolTip=t.Choose("Центрировать на последней позиции игрока","Follow last confirmed player position");
        EditHint.Text=_preferences.HudLocked ? t.Choose($"{_preferences.LockHotkey} · настройка     {_preferences.MapHotkey} · скрыть карту",$"{_preferences.LockHotkey} · edit     {_preferences.MapHotkey} · hide map") :
            t.Choose("Перетаскивай карту · колесо — масштаб · угол — размер","Drag map to pan · wheel to zoom · corner to resize");
        _optionsSignature="";
        WorldPicker.ItemsSource=WorldOptions();
        if(!_database.Worlds.ContainsKey(_preferences.MapWorld))_preferences.MapWorld=1110;
        WorldPicker.SelectedValue=_preferences.MapWorld;
        Map.SetWorld(_database.Worlds[_preferences.MapWorld]);
        _changing=false;HelpBadge.TranslateTree(this,_language);UpdateFollowButton();KeepOnScreen();RenderData();
    }
    private IEnumerable<int> ChosenTargets()=>_preferences.FocusMode ?
        (_collection is null ? [] : HudProjection.Focus(_collection,_categories).Visible.Select(x=>x.Id)) :
        _preferences.MapPetId>0 ? new[]{_preferences.MapPetId} : _preferences.HudTargets;
    private bool ShowAll=>!_preferences.FocusMode && _preferences.MapAllIncomplete;
    private void ToggleSettings(object sender,RoutedEventArgs e)
    {
        _preferences.MapSettingsExpanded=!_preferences.MapSettingsExpanded;
        ApplySettingsVisibility();PreferencesChanged?.Invoke();
    }
    private void ApplySettingsVisibility()
    {
        var visibility=_preferences.MapSettingsExpanded ? Visibility.Visible : Visibility.Collapsed;
        WorldSettings.Visibility=TargetSettings.Visibility=MapDetails.Visibility=EditHint.Visibility=visibility;
        SettingsButton.Content=_preferences.MapSettingsExpanded ? "⌃" : "⋯";
    }
    private WorldOption[] WorldOptions()
    {
        var t=new UiText(_language);
        return _database.Worlds.OrderBy(x=>x.Value.Group).ThenBy(x=>x.Key).Select(x=>new WorldOption(x.Key,
            x.Value.Name+" · "+(x.Value.Group switch {"elyos"=>t.Choose("Элийцы","Elyos"),"asmodians"=>t.Choose("Асмодиане","Asmodians"),_=>t.Choose("Бездна","Abyss")})+
            (_collection is null ? "" : $" · {_database.Select(x.Key,_collection,ChosenTargets(),ShowAll,_categories,!_preferences.FocusMode).Count}"))).ToArray();
    }
    private void UpdateOptions()
    {
        var signature=$"{_collection!.LastTimeMs}:{_collection.HasSnapshot}:{_language}:{_preferences.FocusMode}:{_preferences.MapAllIncomplete}:{_preferences.MapPetId}:{string.Join(',',_preferences.HudTargets)}:{string.Join(',',_categories)}";
        if(signature==_optionsSignature)return;
        _changing=true;
        var eligible=_collection.Pets.Where(x=>x.IsRegular && !x.Completed && _preferences.HudTargets.Contains(x.Id)).ToArray();
        if(!_preferences.FocusMode && !eligible.Any(x=>x.Id==_preferences.MapPetId))_preferences.MapPetId=0;
        var t=new UiText(_language);
        TargetPicker.ItemsSource=new[]{new WorldOption(0,_preferences.FocusMode ? t.Choose("Фокус: ближайшие к MAX","Focus: closest to MAX") : _preferences.HudTargets.Length==0 ?
            t.Choose("Все незавершённые по категориям","All incomplete by category") : t.Choose("Отмеченные цели HUD","Pinned HUD targets"))}
            .Concat(eligible.OrderBy(x=>PetText.Name(x,_language)).Select(x=>new WorldOption(x.Id,PetText.Name(x,_language)))).ToArray();
        TargetPicker.SelectedValue=_preferences.FocusMode ? 0 : _preferences.MapPetId;
        TargetPicker.IsEnabled=!_preferences.FocusMode && !_preferences.MapAllIncomplete && eligible.Length>0;
        WorldPicker.ItemsSource=WorldOptions();WorldPicker.SelectedValue=_preferences.MapWorld;
        Legend.Children.Clear();
        foreach(var category in _categories)
            Legend.Children.Add(new TextBlock {Text="● "+PetText.Category(category,_language),Foreground=MapCanvas.CategoryBrush(category),FontSize=10,Margin=new Thickness(0,0,10,0)});
        _changing=false;
        _optionsSignature=signature;
    }
    internal void Refresh(CollectionView collection,NavigationView navigation,IEnumerable<string> categories)
    {
        _collection=collection;_navigation=navigation;_categories=categories.ToArray();
        FollowWorld();RenderData();
    }
    internal void RefreshNavigation(NavigationView navigation)
    {
        if(_navigation==navigation)return;
        _navigation=navigation;
        if(FollowWorld())RenderData();else RenderPosition();
    }
    private bool FollowWorld()
    {
        if(_preferences.MapFollowPlayer && _navigation.WorldId is int world && _database.Worlds.ContainsKey(world) && _preferences.MapWorld!=world)
        {
            _preferences.MapWorld=world;_changing=true;WorldPicker.SelectedValue=world;_changing=false;
            Map.SetWorld(_database.Worlds[world]);PreferencesChanged?.Invoke();
            return true;
        }
        return false;
    }
    private void RenderData()
    {
        if(_collection is null)return;
        UpdateOptions();
        var t=new UiText(_language);var world=_preferences.MapWorld;
        var targets=ChosenTargets().ToArray();
        var byCategory=!_preferences.FocusMode && (ShowAll || targets.Length==0);
        var points=_database.Select(world,_collection,targets,ShowAll,_categories,!_preferences.FocusMode);
        SpawnCount=points.Count;
        Map.SetSpawns(points,_collection.Pets,_language);
        var eligible=SpawnDatabase.FilterTargets(_collection,targets,ShowAll,_categories,!_preferences.FocusMode);
        MapScope.Text=_preferences.FocusMode ? t.Choose("Фокус · ближайшие к MAX","Focus · closest to MAX") : byCategory ? t.Choose("По категориям · без MAX","By category · excluding MAX") :
            t.Choose("Выбранные цели · без MAX","Selected targets · excluding MAX");
        MapScope.ToolTip=_preferences.FocusMode ? t.Choose("Те же ближайшие к MAX цели, что в счётчике. При равенстве показаны все лидеры.","The same closest-to-MAX targets as the counter. All tied leaders are shown.") : t.Choose("Категории: ","Categories: ")+string.Join(", ",_categories.Select(x=>PetText.Category(x,_language)))+
            (byCategory ? t.Choose(". На карте — все незавершённые цели этих категорий.",". The map shows all incomplete targets in these categories.") :
                t.Choose(". Сними выбор всех целей HUD, чтобы увидеть всех незавершённых по категориям.",". Unpin all HUD targets to show all incomplete targets by category."));
        var missing=eligible.Count(x=>!_database.Pets.TryGetValue(x.Id,out var drops) || !drops.Any(d=>d.Worlds.Any(w=>w.Points.Length>0)));
        SpawnSummary.Text=t.Choose($"{points.Count:N0} мест · {points.Select(x=>x.PetId).Distinct().Count()} целей на территории",
            $"{points.Count:N0} locations · {points.Select(x=>x.PetId).Distinct().Count()} targets here")+
            (missing>0 ? t.Choose($" · нет координат: {missing}",$" · no coordinates: {missing}") : "")+
            (_collection.NeedsResync ? t.Choose(" · прогресс сохранён, нужен вход"," · saved progress, login required") : "");
        EmptyPanel.Visibility=points.Count==0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text=!_collection.HasSnapshot ? t.Choose("Войди в персонажа, чтобы загрузить коллекцию.","Log in to load the collection.") : _categories.Length==0 ?
            t.Choose("Выбери категории в каталоге.","Enable categories in the collection.") : eligible.Count==0 ?
            _preferences.FocusMode ? t.Choose("Нет незавершённых целей с известным прогрессом в выбранных категориях.","No incomplete targets with known progress in enabled categories.") : byCategory ? t.Choose("В этих категориях все цели уже MAX.","All targets in these categories are already MAX.") :
                t.Choose("Среди выбранных целей нет незавершённых в этих категориях.","No selected targets are incomplete in these categories.") :
            t.Choose("Здесь нет известных точек этих целей. Проверь другие территории в списке.","No known locations for these targets here. Check other territories above.");
        RenderPosition();UpdateFollowButton();
    }
    private void RenderPosition()
    {
        var t=new UiText(_language);var world=_preferences.MapWorld;
        var age=Math.Max(0,(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-_navigation.TimeMs)/1000);
        var here=_navigation.HasPosition && _navigation.WorldId==world;
        var fresh=here && !_navigation.IsReplay && age<=8;
        var position=here ? _navigation : _navigation with {X=null,Y=null};
        Map.UpdatePosition(position,fresh,_preferences.MapFollowPlayer);
        PositionStatus.Foreground=fresh ? new SolidColorBrush(Color.FromRgb(114,255,224)) : new SolidColorBrush(Color.FromRgb(242,204,135));
        var ageText=age<60 ? t.Choose($"{age} с",$"{age}s") : age<3600 ? t.Choose($"{age/60} мин",$"{age/60}m") : t.Choose($"{age/3600} ч",$"{age/3600}h");
        PositionStatus.Text=here ? _navigation.IsReplay ? t.Choose("Позиция из записи · не текущая","Recorded position · not live") :
            fresh ? t.Choose($"Позиция подтверждена · {ageText} назад",$"Position confirmed · {ageText} ago") :
            t.Choose($"Последняя позиция · {ageText} назад · ждём новые координаты",$"Last position · {ageText} ago · waiting for coordinates") :
            _navigation.WorldId.HasValue && _navigation.WorldId!=world ? t.Choose("Игрок на другой территории · «Я» вернёт к нему","Player is in another territory · Me returns there") :
            _navigation.ActorId.HasValue ? t.Choose("После пропуска данных ждём подтверждение зоны","Waiting for the zone after a stream gap") :
            t.Choose("Позиция неизвестна · после запуска нужен вход в персонажа","Position unknown · log in after starting the tracker");
        if(_navigation.WorldId is int unsupported && !_database.Worlds.ContainsKey(unsupported))
            PositionStatus.Text=t.Choose("Для текущей зоны пока нет карты · выбери территорию вручную","No map for the current zone · choose a territory manually");
        PositionStatus.ToolTip=PositionStatus.Text;
        if(here && !_navigation.IsReplay)
            PositionStatus.Text=fresh ? t.Choose($"Позиция: {ageText} назад",$"Position: {ageText} ago") :
                t.Choose($"Позиция устарела · {ageText} назад",$"Position is stale · {ageText} ago");
    }
    private void UpdateFollowButton()=>PlayerButton.Background=new SolidColorBrush(_preferences.MapFollowPlayer ? Color.FromRgb(43,100,96) : Color.FromRgb(37,54,75));
    private void WorldChanged(object sender,SelectionChangedEventArgs e)
    {
        if(_changing || WorldPicker.SelectedValue is not int id)return;
        _preferences.MapWorld=id;_preferences.MapFollowPlayer=false;
        Map.SetWorld(_database.Worlds[id]);RenderData();PreferencesChanged?.Invoke();
    }
    private void ModeChanged(object sender,RoutedEventArgs e)
    {
        if(_changing)return;
        _preferences.MapAllIncomplete=AllIncomplete.IsChecked==true;RenderData();PreferencesChanged?.Invoke();
    }
    private void TargetChanged(object sender,SelectionChangedEventArgs e)
    {
        if(_changing || TargetPicker.SelectedValue is not int id)return;
        _preferences.MapPetId=id;RenderData();PreferencesChanged?.Invoke();
    }
    private void FollowPlayer(object sender,RoutedEventArgs e)
    {
        _preferences.MapFollowPlayer=true;
        if(_collection is not null)Refresh(_collection,_navigation,_categories);
        PreferencesChanged?.Invoke();
    }
    private void FitMap(object sender,RoutedEventArgs e)=>Map.Fit();
    private void ZoomIn(object sender,RoutedEventArgs e)=>Map.ZoomBy(1.25);
    private void ZoomOut(object sender,RoutedEventArgs e)=>Map.ZoomBy(.8);
    private void ToggleLock(object sender,RoutedEventArgs e)=>LockRequested?.Invoke();
    private void HideMap(object sender,RoutedEventArgs e)=>HideRequested?.Invoke();
    private void SaveGeometry()
    {
        _preferences.MapWidth=Width;_preferences.MapHeight=Height;
        _preferences.MapLeft=Left;_preferences.MapTop=Top;PreferencesChanged?.Invoke();
    }
    private void KeepOnScreen()
    {
        if(!IsLoaded || _preview)return;
        var minLeft=SystemParameters.VirtualScreenLeft;var minTop=SystemParameters.VirtualScreenTop;
        if(Left+80<minLeft || Left+Width-80>minLeft+SystemParameters.VirtualScreenWidth ||
            Top+50<minTop || Top+50>minTop+SystemParameters.VirtualScreenHeight)
        {Left=SystemParameters.WorkArea.Left+30;Top=SystemParameters.WorkArea.Top+100;}
    }
    private void DragHeader(object sender,MouseButtonEventArgs e)
    {
        if(_preferences.HudLocked || e.LeftButton!=MouseButtonState.Pressed)return;
        for(var node=e.OriginalSource as DependencyObject;node is not null;
            node=node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if(node is ButtonBase)return;
        DragMove();SaveGeometry();
    }
}
