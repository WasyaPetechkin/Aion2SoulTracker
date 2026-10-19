using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SoulTracker.Core;

namespace SoulTracker.App;

public partial class HudWindow : Window
{
    private nint _handle;
    private bool _locked;
    private string _language="ru";
    private string _signature="";
    private readonly bool _preview;
    internal event Action? CatalogRequested;
    internal event Action? HideRequested;
    internal event Action? LockRequested;
    internal event Action? GeometryChanged;
    internal sealed record TargetRow(string Name,string Detail,int Souls,int Target,string Remaining,string RemainingCaption);

    internal HudWindow(AppPreferences preferences,bool preview=false)
    {
        _preview=preview;
        InitializeComponent();
        Width=preferences.HudWidth;
        Height=preferences.HudHeight;
        PanelBackground.Opacity=preferences.HudOpacity;
        RowScaleTransform.ScaleX=RowScaleTransform.ScaleY=preferences.HudRowScale;
        _locked=preferences.HudLocked;
        Left=preferences.HudLeft ?? SystemParameters.WorkArea.Right-Width-28;
        Top=preferences.HudTop ?? SystemParameters.WorkArea.Top+100;
        SourceInitialized+=(_,_) =>
        {
            _handle=new WindowInteropHelper(this).Handle;
            OverlayNative.SetClickThrough(_handle,_locked);
        };
        Loaded+=(_,_)=>KeepOnScreen();
        OverlaySizing.Attach(this,ResizeCorner,()=>_locked,()=>GeometryChanged?.Invoke());
    }
    internal void Configure(AppPreferences preferences,string language)
    {
        _language=language;
        _locked=preferences.HudLocked;
        Width=preferences.HudWidth;
        Height=preferences.HudHeight;
        PanelBackground.Opacity=preferences.HudOpacity;
        if(_handle!=0) OverlayNative.SetClickThrough(_handle,_locked);
        var t=new UiText(language);
        HudTitle.Text=preferences.FocusMode ? t.Choose("ФОКУС","FOCUS") : t.Choose("ДУШИ","SOULS");
        CatalogButton.ToolTip=t.Choose("Открыть каталог","Open collection");
        HideButton.ToolTip=t.Choose("Скрыть HUD · ","Hide HUD · ")+preferences.HudHotkey;
        LockButton.Content=_locked ? "◆" : "◇";
        LockButton.ToolTip=t.Choose("Клики в игру · ","Click through · ")+preferences.LockHotkey;
        EditState.Text=_locked ? t.Choose("ЗАКРЕПЛЁН · клики проходят в игру","LOCKED · clicks pass to the game") :
            t.Choose("НАСТРОЙКА · перемещение за заголовок, размер за угол","EDIT MODE · drag header to move, corner to resize");
        ResizeCorner.Visibility=_locked ? Visibility.Collapsed : Visibility.Visible;
        HotkeyHint.Text=t.Choose($"{preferences.HudHotkey} · скрыть    {preferences.LockHotkey} · клики в игру",$"{preferences.HudHotkey} · show/hide    {preferences.LockHotkey} · click through");
        RowScaleTransform.ScaleX=RowScaleTransform.ScaleY=preferences.HudRowScale;
        HelpBadge.TranslateTree(this,language);
        DragSurface.Cursor=_locked ? Cursors.Arrow : Cursors.SizeAll;
        KeepOnScreen();
    }
    internal void Refresh(CollectionView view,AppPreferences preferences,IEnumerable<string>? categories=null)
    {
        var t=new UiText(_language);
        var regular=view.Pets.Where(x=>x.IsRegular).ToArray();
        CollectionSummary.Text=view.HasSnapshot ? $"{regular.Count(x=>x.Level>0)} / {regular.Length}  ·  MAX {regular.Count(x=>x.Completed)}" : "— / 200";
        SyncState.Text=!view.HasSnapshot ? t.Choose("Ожидание","Waiting") : view.NeedsResync ? t.Choose("Нужен вход","Login required") :
            view.Source=="Живые данные игры" ? t.Choose("Онлайн","Live") : t.Choose("Запись","Recording");
        SyncState.Foreground=new SolidColorBrush(view.HasSnapshot && !view.NeedsResync ? Color.FromRgb(121,215,195) : Color.FromRgb(216,181,115));
        var projected=preferences.FocusMode ? HudProjection.Focus(view,categories ?? new[]{"Cogni","Fera","Natura","Varian"}) : HudProjection.Select(view,preferences.HudTargets,preferences.HudRows);
        var sig=$"{view.LastTimeMs}:{view.HasSnapshot}:{view.NeedsResync}:{_language}:{string.Join(',',preferences.HudTargets)}:{preferences.HudRows}:{preferences.FocusMode}:{string.Join(',',projected.Visible.Select(x=>x.Id))}";
        if(_signature==sig) return;
        _signature=sig;
        Targets.ItemsSource=projected.Visible.Select(x=>new TargetRow(PetText.Name(x,_language),
            $"{PetText.Level(x,_language)}  ·  {x.Stage}",Math.Min(x.Souls,x.Target),Math.Max(1,x.Target),
            PetText.Remaining(x,_language),t.Choose("до MAX","to MAX"))).ToArray();
        EmptyPanel.Visibility=projected.Visible.Count==0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text=!view.HasSnapshot ? t.Choose("Ожидаем коллекцию","Waiting for collection") : projected.CompletedCount>0 ?
            t.Choose("Все выбранные цели — MAX","All selected targets are MAX") : t.Choose("Выбери цели для фарма","Choose farming targets");
        EmptyHint.Text=!view.HasSnapshot ? t.Choose("Войди в персонажа, чтобы получить прогресс.","Log in to load your progress.") :
            t.Choose("Отметь нужных питомцев в колонке HUD в каталоге.","Check the HUD column next to pets in the collection.");
        if(preferences.FocusMode && view.HasSnapshot)
        {
            EmptyTitle.Text=t.Choose("Нет целей для фокуса","No focus targets");
            EmptyHint.Text=t.Choose("Проверь категории: все подходящие цели завершены или ожидают синхронизации.","Check categories: eligible targets are complete or waiting for synchronization.");
        }
        MoreTargets.Text=projected.ExtraCount>0 ? t.Choose($"Ещё целей: {projected.ExtraCount} · ближние к MAX показаны первыми",$"{projected.ExtraCount} more targets · closest to MAX shown first") : "";
        MoreTargets.Visibility=projected.ExtraCount>0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void KeepOnScreen()
    {
        if(!IsLoaded || _preview) return;
        var minLeft=SystemParameters.VirtualScreenLeft;
        var minTop=SystemParameters.VirtualScreenTop;
        var maxRight=minLeft+SystemParameters.VirtualScreenWidth;
        var maxBottom=minTop+SystemParameters.VirtualScreenHeight;
        // Recover positions after a monitor was disconnected; allow normal placement on any display.
        if(Left+80<minLeft || Left+Width-80>maxRight || Top+50<minTop || Top+50>maxBottom)
        {
            Left=Math.Max(SystemParameters.WorkArea.Left,SystemParameters.WorkArea.Right-Width-28);
            Top=SystemParameters.WorkArea.Top+100;
        }
    }
    private void DragHeader(object sender,MouseButtonEventArgs e)
    {
        if(_locked || e.LeftButton!=MouseButtonState.Pressed) return;
        for(var node=e.OriginalSource as DependencyObject;node is not null;
            node=node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if(node is ButtonBase) return;
        DragMove();
        GeometryChanged?.Invoke();
    }
    private void OpenCatalog(object sender,RoutedEventArgs e)=>CatalogRequested?.Invoke();
    private void HideHud(object sender,RoutedEventArgs e)=>HideRequested?.Invoke();
    private void ToggleLock(object sender,RoutedEventArgs e)=>LockRequested?.Invoke();
}
