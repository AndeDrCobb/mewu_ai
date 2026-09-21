// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using mewu_ai_Assistant.Services;

namespace mewu_ai_Assistant.Views;

/// <summary>A small restore surface for a minimized screenshot conversation.</summary>
internal sealed class ConversationFloatingWidget : Window
{
    private readonly CaptureOverlayWindow _owner;



    internal ConversationFloatingWidget(CaptureOverlayWindow owner)
    {
        _owner=owner??throw new ArgumentNullException(nameof(owner));
        Title=LocalizationService.T("喵呜AI 对话","MewuAI conversation");
        Width=206;Height=54;MinWidth=180;MinHeight=48;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;
        ShowActivated=true;
        Content=BuildContent();
        Loaded+=(_,_)=>PlaceNearWorkArea();
        MouseLeftButtonDown+=OnMouseDown;
    }

    private FrameworkElement BuildContent()
    {
        var shell=new Border{Background=new SolidColorBrush(Color.FromRgb(249,251,255)),BorderBrush=new SolidColorBrush(Color.FromRgb(211,220,235)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(18),Padding=new Thickness(9,6,9,6)};
        var row=new DockPanel{LastChildFill=true};
        var close=new Button{Name="CloseConversationButton",Style=(Style)_owner.FindResource("ConversationHeaderButton"),Width=22,Height=22,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(5,0,0,0),ToolTip=LocalizationService.T("关闭会话","Close conversation"),Visibility=Visibility.Hidden};
        close.Content=new System.Windows.Shapes.Path{Width=12,Height=12,Stroke=new SolidColorBrush(Color.FromRgb(113,126,151)),StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Data=Geometry.Parse("M3,3 L9,9 M9,3 L3,9")};
        System.Windows.Automation.AutomationProperties.SetName(close,LocalizationService.T("关闭会话","Close conversation"));
        MouseEnter+=(_,_)=>close.Visibility=Visibility.Visible;
        MouseLeave+=(_,_)=>close.Visibility=Visibility.Hidden;
        close.Click+=(_,e)=>{e.Handled=true;_owner.Close();};DockPanel.SetDock(close,Dock.Right);row.Children.Add(close);
        var icon=new Border{Width=28,Height=28,CornerRadius=new CornerRadius(10),Background=new SolidColorBrush(Color.FromRgb(232,237,255)),Margin=new Thickness(0,0,8,0)};
        icon.Child=new System.Windows.Shapes.Path{Width=15,Height=15,Stretch=Stretch.Uniform,Stroke=new SolidColorBrush(Color.FromRgb(82,99,217)),StrokeThickness=1.5,Data=Geometry.Parse("M2,2 L14,2 L14,11 L8,11 L4,14 L4,11 L2,11 Z M5,5 L11,5 M5,8 L9,8"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
        var text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
        text.Children.Add(new TextBlock{Text=LocalizationService.T("截图会话","Screenshot conversation"),Foreground=new SolidColorBrush(Color.FromRgb(45,59,84)),FontSize=12.5,FontWeight=FontWeights.SemiBold});
        text.Children.Add(new TextBlock{Text=LocalizationService.T("点击恢复对话","Click to restore"),Foreground=new SolidColorBrush(Color.FromRgb(116,130,153)),FontSize=10.5,Margin=new Thickness(0,1,0,0)});
        row.Children.Add(text);
        shell.Child=row;return shell;
    }

    private void OnMouseDown(object? sender,MouseButtonEventArgs e)
    {
        for(var source=e.OriginalSource as DependencyObject;source is not null;source=VisualTreeHelper.GetParent(source))
            if(source is Button)return;
        if(e.ChangedButton==MouseButton.Left){_owner.RestoreFromConversationWidget();e.Handled=true;}
    }

    private void PlaceNearWorkArea()
    {
        var area=SystemParameters.WorkArea;
        var index=Application.Current?.Windows.OfType<ConversationFloatingWidget>().Count(window=>window.IsVisible&&!ReferenceEquals(window,this))??0;
        var column=index%4;var row=index/4;
        Left=Math.Max(area.Left+8,area.Right-Width-18-column*(Width+10));
        Top=Math.Max(area.Top+8,area.Bottom-Height-22-row*(Height+10));
    }

    internal void CloseForOwnerExit()
    {
        try{Close();}catch{}
    }
}
