using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SoulTracker.App;

internal static class OverlayNative
{
    internal const int GwlExStyle=-20;
    internal const long Transparent=0x20,ToolWindow=0x80,Layered=0x80000,NoActivate=0x08000000;
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]
    internal static extern nint GetWindowLongPtr(nint window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]
    private static extern nint SetWindowLongPtr(nint window,int index,nint value);
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint window,int id);
    [DllImport("user32.dll")]
    internal static extern nint SendMessageW(nint window,int message,nint wParam,nint lParam);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint window);

    internal static void SetClickThrough(nint window,bool locked)
    {
        var style=(long)GetWindowLongPtr(window,GwlExStyle) | ToolWindow | NoActivate | Layered;
        style=locked ? style | Transparent : style & ~Transparent;
        Marshal.SetLastPInvokeError(0);
        var previous=SetWindowLongPtr(window,GwlExStyle,(nint)style);
        var error=Marshal.GetLastPInvokeError();
        if(previous==0 && error!=0) throw new System.ComponentModel.Win32Exception(error);
    }
}

internal sealed class HudHotkeys : IDisposable
{
    internal sealed record Binding(int Id,HotkeySpec Keys,Action Action);
    private readonly nint _handle;
    private readonly HwndSource _source;
    private readonly Dictionary<int,Action> _actions=[];
    private readonly List<Binding> _bindings=[];
    internal HudHotkeys(nint handle)
    {
        _handle=handle;
        _source=HwndSource.FromHwnd(handle)!;
        _source.AddHook(Hook);
    }
    internal bool Register(int id,uint key,Action callback)
    {
        return Register(new(id,new(3,key,""),callback));
    }
    private bool Register(Binding binding)
    {
        if(!OverlayNative.RegisterHotKey(_handle,binding.Id,binding.Keys.Modifiers|0x4000,binding.Keys.VirtualKey)) return false;
        _actions.Add(binding.Id,binding.Action);_bindings.Add(binding);
        return true;
    }
    internal bool Replace(IReadOnlyList<Binding> bindings,out string error,out bool rollbackSucceeded)
    {
        error="";rollbackSucceeded=true;
        if(bindings.Select(x=>(x.Keys.Modifiers,x.Keys.VirtualKey)).Distinct().Count()!=bindings.Count)
        {error="duplicate";return false;}
        var previous=_bindings.ToArray();UnregisterAll();
        foreach(var binding in bindings)
        {
            if(Register(binding))continue;
            error=binding.Keys.Text;UnregisterAll();
            foreach(var old in previous)if(!Register(old))rollbackSucceeded=false;
            return false;
        }
        return true;
    }
    private void UnregisterAll()
    {
        foreach(var id in _actions.Keys)OverlayNative.UnregisterHotKey(_handle,id);
        _actions.Clear();_bindings.Clear();
    }
    private nint Hook(nint hwnd,int message,nint wParam,nint lParam,ref bool handled)
    {
        if(message==0x0312 && _actions.TryGetValue((int)wParam,out var callback))
        { callback(); handled=true; }
        return 0;
    }
    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(Hook);
    }
}
