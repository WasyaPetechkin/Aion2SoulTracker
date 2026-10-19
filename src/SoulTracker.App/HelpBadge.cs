using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace SoulTracker.App;

public sealed class HelpBadge : Border
{
    public string Topic {get;set;}="";
    public HelpBadge()
    {
        Width=16;Height=16;CornerRadius=new CornerRadius(8);BorderThickness=new Thickness(1);
        BorderBrush=new SolidColorBrush(Color.FromRgb(105,137,157));Background=new SolidColorBrush(Color.FromRgb(28,45,62));
        Margin=new Thickness(5,0,8,0);VerticalAlignment=VerticalAlignment.Center;Cursor=Cursors.Help;Focusable=true;
        Child=new TextBlock {Text="?",FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(175,212,218)),
            HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        ToolTipService.SetInitialShowDelay(this,150);ToolTipService.SetShowDuration(this,30000);
        ToolTipService.SetShowOnDisabled(this,true);
        PreviewMouseLeftButtonDown+=(_,e)=>{if(ToolTip is ToolTip tip)tip.IsOpen=true;e.Handled=true;};
        GotKeyboardFocus+=(_,_)=>{if(ToolTip is ToolTip tip)tip.IsOpen=true;};
        LostKeyboardFocus+=(_,_)=>{if(ToolTip is ToolTip tip)tip.IsOpen=false;};
    }
    internal void Translate(string language)
    {
        var text=HelpText.Get(Topic,language);
        ToolTip=new ToolTip{Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,MaxWidth=310},PlacementTarget=this};
        AutomationProperties.SetName(this,(language=="ru" ? "Справка: ":"Help: ")+text);
    }
    internal static void TranslateTree(DependencyObject root,string language)
    {
        if(root is HelpBadge badge){badge.Translate(language);return;}
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())TranslateTree(child,language);
    }
}
