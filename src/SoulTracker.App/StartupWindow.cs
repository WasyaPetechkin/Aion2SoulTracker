using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SoulTracker.App;

internal static class StartupWindow
{
    // WPF's first Show can inherit STARTUPINFO's hidden state (for example from a launcher).
    // Explicitly show the HWND after WPF has created it, including on a second launch.
    internal static bool ShowVisible(Window window,bool activate=true)
    {
        if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;
        window.Show();
        var handle=new WindowInteropHelper(window).EnsureHandle();
        ShowWindow(handle,IsIconic(handle) ? (activate ? 9 : 4) : (activate ? 5 : 8));
        if(activate){window.Activate();SetForegroundWindow(handle);}
        return IsWindowVisible(handle) && !IsIconic(handle);
    }

    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd,int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
}
