using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls.Primitives;
using AionMeter.Core.Capture;
using SoulTracker.Core;
namespace SoulTracker.App;

public partial class MainWindow
{
    private void VerifyDiagnostics(string directory,Dictionary<string,bool> checks)
    {
        void Check(string name,bool value)=>checks.Add(name,value);
        var now=DateTimeOffset.UtcNow;var stamp=now.ToUnixTimeMilliseconds();
        var capture=new CaptureDiagnostics(1,2,1,1,1,stamp-1000,2500,800,2,stamp-500,0,0,0,0,CaptureState.Capturing,CaptureIssue.None);
        var collection=new CollectionDiagnostics(true,true,true,0,stamp-60000,0,0,0,0,StreamIssue.None,DiagnosticRoute.Loopback);
        var current=DiagnosticReport.Create(new Version(0,3,9),now,DiagnosticMode.Live,true,capture,collection,700,0,[]);
        var window=new DiagnosticsWindow(()=>current,"ru") {WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000,ShowActivated=false};
        window.Show();window.UpdateLayout();
        Check("diagnostics_live_timer_started",window.Refreshing);
        Check("diagnostics_missing_gain_is_not_failure",window.Summary.Text.Contains("коллекция синхронизирована") && ((DiagnosticStep)window.Steps.Items[6]).Status.Contains("не ошибка"));
        Render(window,Path.Combine(directory,"diagnostics-ru.png"));
        var target=Path.Combine(directory,"diagnostic-report.json");
        var refused=false;try{window.SavePreparedReport(target);}catch(InvalidOperationException){refused=true;}
        Check("diagnostics_requires_preview_before_export",refused && !File.Exists(target));
        window.PrepareButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var prepared=window.PreparedReport;
        Check("diagnostics_preview_stops_refresh",!window.Refreshing && window.PreviewPanel.IsVisible && !window.LivePanel.IsVisible && prepared is not null);
        current=current with{Collection=current.Collection with{Trusted=false,LastLoss=StreamIssue.TcpGap,LossAgeSeconds=0}};
        window.RefreshReport();window.SavePreparedReport(target);
        using(var json=JsonDocument.Parse(File.ReadAllText(target)))
            Check("diagnostics_saves_exact_frozen_preview",File.ReadAllText(target)==prepared && window.PreviewText.Text==prepared && json.RootElement.GetProperty("Outcome").GetString()=="Ready");
        window.Width=window.MinWidth;window.Height=window.MinHeight;
        Render(window,Path.Combine(directory,"diagnostics-preview-small.png"));
        Check("diagnostics_small_preview_controls_visible",window.CopyButton.IsVisible && window.CloseButton.IsVisible && window.PreviewText.ActualHeight>100);
        window.BackButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check("diagnostics_back_refreshes_current_state",window.Refreshing && window.PreparedReport is null && window.Summary.Text=="Нужна повторная синхронизация");
        window.Width=880;window.Height=850;Render(window,Path.Combine(directory,"diagnostics-resync.png"));
        current=current with{NpcapInstalled=false};window.RefreshReport();
        Check("diagnostics_missing_driver_guidance",window.Summary.Text.Contains("Npcap") && window.ActionHint.Text.Contains("Проверить снова"));
        Render(window,Path.Combine(directory,"diagnostics-npcap.png"));
        current=current with{Mode=DiagnosticMode.Offline,NpcapInstalled=true};window.RefreshReport();
        Check("diagnostics_saved_mode_explicit",window.Summary.Text=="Просмотр сохранённых данных" && ((DiagnosticStep)window.Steps.Items[0]).Status=="Не проверяется");
        window.Close();Check("diagnostics_closed_timer_stopped",!window.Refreshing);
        current=current with{Mode=DiagnosticMode.Live,Collection=current.Collection with{Trusted=true},Capture=current.Capture! with{TrafficAgeSeconds=20}};
        var english=new DiagnosticsWindow(()=>current,"en",false){WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000,ShowActivated=false};
        english.Show();english.UpdateLayout();
        Check("diagnostics_quiet_session_english",english.Summary.Text=="Waiting for game traffic" && english.ActionHint.Text.Contains("does not require another login"));
        Render(english,Path.Combine(directory,"diagnostics-en.png"));english.Close();
        var failing=new DiagnosticsWindow(()=>throw new InvalidOperationException("PRIVATE_ERROR_DETAIL"),"ru",false){WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000,ShowActivated=false};
        failing.Show();failing.PrepareReport();
        Check("diagnostics_read_error_hides_raw_detail",failing.PreparedReport is null && failing.Notice.Text.Contains("Не удалось") && !failing.Notice.Text.Contains("PRIVATE_ERROR"));failing.Close();
        var originalWidth=Width;Width=MinWidth;UpdateLayout();
        Render(this,Path.Combine(directory,"collection-diagnostics-small.png"));Width=originalWidth;
    }
}
