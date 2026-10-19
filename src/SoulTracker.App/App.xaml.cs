using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
namespace SoulTracker.App;
public partial class App : Application
{
    internal static string DataDirectory {get;private set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aion2SoulTracker");
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showRequest;
    private RegisteredWaitHandle? _showWait;
    private bool _ownsMutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var verify=Array.IndexOf(e.Args,"--verify");
        try
        {
            var data=Array.IndexOf(e.Args,"--data-dir");
            if(data>=0 && data+1<e.Args.Length)DataDirectory=Path.GetFullPath(e.Args[data+1]);
            if(verify>=0 && verify+1<e.Args.Length)DataDirectory=Path.Combine(Path.GetFullPath(e.Args[verify+1]),"state");
            Directory.CreateDirectory(DataDirectory);
            if(verify<0 && !AcquireInstance()){Shutdown();return;}
            var window=new MainWindow(e.Args);MainWindow=window;ShutdownMode=ShutdownMode.OnMainWindowClose;
            if(verify>=0 && verify+1<e.Args.Length)
            {
                ShutdownMode=ShutdownMode.OnExplicitShutdown;
                var output=Path.GetFullPath(e.Args[verify+1]);
                window.ShowActivated=false;window.Left=-10000;window.Top=-10000;window.Show();
                Dispatcher.InvokeAsync(()=>
                {
                    try {window.Verify(output);window.Close();Shutdown();}
                    catch(Exception ex){File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());window.Close();Shutdown(1);}
                },DispatcherPriority.ContextIdle);
            }
            else StartupWindow.ShowVisible(window);
        }
        catch(Exception ex)
        {
            try {File.WriteAllText(Path.Combine(DataDirectory,"startup-error.txt"),ex.ToString());}catch { }
            if(verify<0)MessageBox.Show("Не удалось запустить Aion 2 Soul Tracker.\n\n"+ex.Message+"\n\nЖурнал: "+DataDirectory,"Aion 2 Soul Tracker",MessageBoxButton.OK,MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    private bool AcquireInstance()
    {
        var identity=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDirectory.ToUpperInvariant())))[..16];
        var name="Local\\Aion2SoulTracker-Collection-"+identity;
        _instanceMutex=new Mutex(true,name,out _ownsMutex);
        if(!_ownsMutex)
        {
            for(var i=0;i<20;i++)
            {
                if(EventWaitHandle.TryOpenExisting(name+"-show",out var signal)){using(signal)signal.Set();break;}
                Thread.Sleep(50);
            }
            return false;
        }
        _showRequest=new(false,EventResetMode.AutoReset,name+"-show");
        _showWait=ThreadPool.RegisterWaitForSingleObject(_showRequest,(_,_)=>Dispatcher.BeginInvoke(new Action(()=>
        {if(MainWindow is not null)StartupWindow.ShowVisible(MainWindow);})),null,Timeout.Infinite,false);
        return true;
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);_showRequest?.Dispose();
        if(_ownsMutex)_instanceMutex?.ReleaseMutex();_instanceMutex?.Dispose();base.OnExit(e);
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        if(MainWindow is MainWindow window)window.PrepareExit();
        base.OnSessionEnding(e);
    }
}
