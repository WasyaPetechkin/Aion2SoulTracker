using System.Windows;
using System.Windows.Controls.Primitives;

namespace SoulTracker.App;
internal static class OverlaySizing
{
    internal static void Attach(Window window,Thumb grip,Func<bool> locked,Action changed)
    {
        grip.DragDelta+=(_,e)=>
        {
            if(locked()) return;
            window.SizeToContent=SizeToContent.Manual;
            window.Width=Math.Clamp(window.ActualWidth+e.HorizontalChange,window.MinWidth,1600);
            window.Height=Math.Clamp(window.ActualHeight+e.VerticalChange,window.MinHeight,1600);
        };
        grip.DragCompleted+=(_,_)=>changed();
    }
}
