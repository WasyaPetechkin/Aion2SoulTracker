using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using SoulTracker.Core;
namespace SoulTracker.App;

public partial class DiagnosticsWindow : Window
{
    private readonly Func<DiagnosticReport> _read;
    private readonly DiagnosticText _text;
    private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromSeconds(1)};
    private readonly bool _autoRefresh;
    private string? _prepared;
    internal bool Refreshing=>_timer.IsEnabled;
    internal string? PreparedReport=>_prepared;
    internal DiagnosticsWindow(Func<DiagnosticReport> read,string language,bool autoRefresh=true)
    {
        InitializeComponent();_read=read;_text=new(language);_autoRefresh=autoRefresh;
        Height=Math.Min(Height,Math.Max(MinHeight,SystemParameters.WorkArea.Height-60));
        Width=Math.Min(Width,Math.Max(MinWidth,SystemParameters.WorkArea.Width-40));
        Title=Heading.Text=_text.T("Диагностика подключения","Connection diagnostics");
        Subtitle.Text=_text.T("От игры до счётчика: состояние обновляется, пока открыто это окно.","From game to counter: status updates while this window is open.");
        PrivacyHint.Text=_text.T("Отчёт содержит только этапы, счётчики и возраст событий. Без имени персонажа, IP-адресов, имён адаптеров и содержимого пакетов.",
            "The report contains stages, counters and event ages only. No character names, IPs, adapter names or packet contents.");
        PreviewHelp.Text=_text.T("Предварительный просмотр: будет сохранён или скопирован именно этот снимок. Он не обновляется автоматически. Чтобы сделать новый, вернитесь к состоянию подключения.",
            "Preview: exactly this snapshot will be saved or copied. It does not update automatically. Go back to connection status to prepare a new one.");
        PrepareButton.Content=_text.T("Подготовить отчёт","Prepare report");BackButton.Content=_text.T("К состоянию","Back to status");
        SaveButton.Content=_text.T("Сохранить JSON","Save JSON");CopyButton.Content=_text.T("Копировать","Copy");CloseButton.Content=_text.T("Закрыть","Close");
        HelpBadge.TranslateTree(this,language);
        _timer.Tick+=(_,_)=>RefreshReport();
        Loaded+=(_,_)=>{RefreshReport();if(_autoRefresh)_timer.Start();};
        Closed+=(_,_)=>_timer.Stop();
    }
    internal void RefreshReport()
    {
        if(_prepared is not null)return;
        try
        {
            var report=_read();var description=_text.Outcome(report.Outcome);
            Summary.Text=description.Title;ActionHint.Text=description.Hint;Steps.ItemsSource=_text.Steps(report);
            Counters.Text=_text.Counters(report);Issue.Text=_text.Issue(report);
            Updated.Text=_text.T("Обновлено: ","Updated: ")+report.CreatedAt.ToLocalTime().ToString("HH:mm:ss")+" · "+report.AppVersion;
            Notice.Text="";
        }
        catch(Exception ex) when(ex is not OutOfMemoryException)
        {Notice.Text=_text.T("Не удалось обновить диагностику. Попробуйте открыть окно снова.","Could not refresh diagnostics. Try reopening this window.");}
    }
    private void PrepareClicked(object sender,RoutedEventArgs e)=>PrepareReport();
    internal void PrepareReport()
    {
        try
        {
            _prepared=_read().ToJson();_timer.Stop();PreviewText.Text=_prepared;
            LivePanel.Visibility=LiveButtons.Visibility=Visibility.Collapsed;
            PreviewPanel.Visibility=PreviewButtons.Visibility=Visibility.Visible;Notice.Text="";
        }
        catch(Exception ex) when(ex is not OutOfMemoryException)
        {Notice.Text=_text.T("Не удалось подготовить отчёт. Попробуйте ещё раз.","Could not prepare the report. Try again.");}
    }
    private void BackClicked(object sender,RoutedEventArgs e)=>BackToStatus();
    internal void BackToStatus()
    {
        _prepared=null;PreviewText.Clear();PreviewPanel.Visibility=PreviewButtons.Visibility=Visibility.Collapsed;
        LivePanel.Visibility=LiveButtons.Visibility=Visibility.Visible;RefreshReport();if(_autoRefresh)_timer.Start();
    }
    internal void SavePreparedReport(string path)
    {
        if(_prepared is null)throw new InvalidOperationException("Prepare and preview a report before saving");
        File.WriteAllText(path,_prepared,new System.Text.UTF8Encoding(false));
    }
    private void SaveClicked(object sender,RoutedEventArgs e)
    {
        if(_prepared is null)return;
        var dialog=new SaveFileDialog{Title=_text.T("Сохранить диагностический отчёт","Save diagnostic report"),
            FileName="Aion2SoulTracker-diagnostics-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json",
            Filter="JSON (*.json)|*.json",DefaultExt=".json",AddExtension=true,OverwritePrompt=true};
        if(dialog.ShowDialog(this)!=true)return;
        try{SavePreparedReport(dialog.FileName);Notice.Text=_text.T("Отчёт сохранён. Его можно приложить к сообщению об ошибке.","Report saved. You can attach it to a bug report.");}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {Notice.Text=_text.T("Не удалось сохранить файл. Выберите другую папку.","Could not save the file. Choose another folder.");}
    }
    private void CopyClicked(object sender,RoutedEventArgs e)
    {
        if(_prepared is null)return;
        try{Clipboard.SetText(_prepared);Notice.Text=_text.T("Отчёт скопирован.","Report copied.");}
        catch(ExternalException){Notice.Text=_text.T("Буфер обмена занят. Попробуйте ещё раз или сохраните файл.","Clipboard is busy. Try again or save the file.");}
    }
    private void CloseClicked(object sender,RoutedEventArgs e)=>Close();
}
