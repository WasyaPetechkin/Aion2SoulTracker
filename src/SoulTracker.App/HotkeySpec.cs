using System.Windows.Input;
namespace SoulTracker.App;

internal sealed record HotkeySpec(uint Modifiers,uint VirtualKey,string Text)
{
    internal static string NormalizeOrDefault(string? text,string fallback)=>TryParse(text,out var value) ? value!.Text : fallback;
    internal static bool TryParse(string? text,out HotkeySpec? value)
    {
        value=null;if(string.IsNullOrWhiteSpace(text))return false;
        var parts=text.Split('+',StringSplitOptions.TrimEntries);
        if(parts.Length==0 || parts.Any(string.IsNullOrEmpty))return false;
        uint mods=0;
        foreach(var part in parts[..^1])
        {
            uint flag=part.ToLowerInvariant() switch {"ctrl" or "control"=>2,"alt"=>1,"shift"=>4,"win" or "windows"=>8,_=>0};
            if(flag==0 || (mods&flag)!=0)return false;mods|=flag;
        }
        var keyName=parts[^1];if(keyName.Length==1 && char.IsAsciiDigit(keyName[0]))keyName="D"+keyName;
        if(!Enum.TryParse<Key>(keyName,true,out var key) || !Enum.IsDefined(key))return false;
        return FromKey(key,mods,out value);
    }
    internal static bool FromKey(Key key,uint modifiers,out HotkeySpec? value)
    {
        value=null;
        if(key is Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed or Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)return false;
        if(modifiers==0 && key is not (>=Key.F1 and <=Key.F24))return false;
        var vk=KeyInterop.VirtualKeyFromKey(key);if(vk<=0 || vk>255)return false;
        var parts=new List<string>();
        if((modifiers&2)!=0)parts.Add("Ctrl");if((modifiers&1)!=0)parts.Add("Alt");
        if((modifiers&4)!=0)parts.Add("Shift");if((modifiers&8)!=0)parts.Add("Win");
        parts.Add(key is >=Key.D0 and <=Key.D9 ? ((int)key-(int)Key.D0).ToString() : key.ToString());
        value=new(modifiers,(uint)vk,string.Join('+',parts));return true;
    }
    internal static uint CurrentModifiers()
    {
        var m=Keyboard.Modifiers;
        return (uint)(((m&ModifierKeys.Control)!=0 ? 2:0)|((m&ModifierKeys.Alt)!=0 ? 1:0)|
            ((m&ModifierKeys.Shift)!=0 ? 4:0)|((m&ModifierKeys.Windows)!=0 ? 8:0));
    }
}
