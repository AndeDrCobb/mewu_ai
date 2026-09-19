// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using mewu_ai_Assistant.Services;

namespace mewu_ai_Assistant.Views;

/// <summary>
/// A normal resizable conversation surface. The prompt bar itself is moved
/// into this window, while the hidden capture overlay retains the frozen frame,
/// selections, drawing state and provider session.
/// </summary>
internal sealed class ConversationWorkspaceWindow : Window
{
    private readonly CaptureOverlayWindow _overlay;
    private readonly Canvas _promptCanvas=new();
    private readonly Image _snapshotImage=new();
    private ConversationFloatingWidget? _widget;
    private bool _closingForRedock,_closingForOwner;

    internal ConversationWorkspaceWindow(CaptureOverlayWindow overlay,BitmapSource snapshot)
    {
        _overlay=overlay??throw new ArgumentNullException(nameof(overlay));
        Title=LocalizationService.T("喵呜AI · 截图会话","MewuAI · Screenshot conversation");
        Width=900;Height=660;MinWidth=680;MinHeight=480;
        WindowStartupLocation=WindowStartupLocation.Manual;
        WindowStyle=WindowStyle.SingleBorderWindow;ResizeMode=ResizeMode.CanResize;
        Background=new SolidColorBrush(Color.FromRgb(245,248,252));
        ShowInTaskbar=true;ShowActivated=true;Topmost=false;
        _snapshotImage.Source=snapshot;_snapshotImage.Stretch=Stretch.Uniform;_snapshotImage.SnapsToDevicePixels=true;
        Content=BuildContent();
        SizeChanged+=(_,_)=>LayoutPrompt();
        Loaded+=(_,_)=>LayoutPrompt();
        Closed+=OnClosed;
        StateChanged+=(_,_)=>{if(WindowState==WindowState.Minimized)MinimizeToWidget();};
    }

    private FrameworkElement BuildContent()
    {
        var root=new Grid{Margin=new Thickness(12,12,12,12)};
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
        var header=new Border{Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(220,228,239)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(14,10,14,10),Margin=new Thickness(0,0,0,10)};
        header.MouseLeftButtonDown+=(_,e)=>{if(e.ChangedButton==MouseButton.Left&&e.ClickCount==2){WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;e.Handled=true;}else if(e.ChangedButton==MouseButton.Left){try{DragMove();}catch(InvalidOperationException){}}};
        var titleRow=new DockPanel{LastChildFill=true};
        var close=HeaderButton("×",LocalizationService.T("关闭会话","Close conversation"));close.Click+=(_,_)=>_overlay.CloseDetachedConversationWindow();DockPanel.SetDock(close,Dock.Right);titleRow.Children.Add(close);
        var minimize=HeaderButton("—",LocalizationService.T("最小化为悬浮窗","Minimize to floating widget"));minimize.Click+=(_,_)=>MinimizeToWidget();DockPanel.SetDock(minimize,Dock.Right);titleRow.Children.Add(minimize);
        var redock=HeaderButton("↙",LocalizationService.T("吸附回截图对话条","Dock back to conversation bar"));redock.Margin=new Thickness(0,0,8,0);redock.Click+=(_,_)=>_overlay.RedockConversationWindow();DockPanel.SetDock(redock,Dock.Right);titleRow.Children.Add(redock);
        var heading=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        var icon=new Border{Width=30,Height=30,CornerRadius=new CornerRadius(10),Background=new SolidColorBrush(Color.FromRgb(232,237,255)),Margin=new Thickness(0,0,9,0)};
        icon.Child=new System.Windows.Shapes.Path{Width=16,Height=16,Stretch=Stretch.Uniform,Stroke=new SolidColorBrush(Color.FromRgb(82,99,217)),StrokeThickness=1.6,Data=Geometry.Parse("M2,2 L14,2 L14,11 L8,11 L4,14 L4,11 L2,11 Z M5,5 L11,5 M5,8 L9,8"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};heading.Children.Add(icon);
        var labels=new StackPanel();labels.Children.Add(new TextBlock{Text=LocalizationService.T("截图会话","Screenshot conversation"),Foreground=new SolidColorBrush(Color.FromRgb(40,53,78)),FontSize=14,FontWeight=FontWeights.SemiBold});labels.Children.Add(new TextBlock{Text=LocalizationService.T("冻结画面、标注和对话已保留 · 可继续提问","Frozen frame, annotations and chat are preserved · Continue asking questions"),Foreground=new SolidColorBrush(Color.FromRgb(111,126,151)),FontSize=10.5,Margin=new Thickness(0,2,0,0)});heading.Children.Add(labels);titleRow.Children.Add(heading);
        header.Child=titleRow;Grid.SetRow(header,0);root.Children.Add(header);

        var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.05,GridUnitType.Star)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.55,GridUnitType.Star)});
        var previewShell=new Border{Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(220,228,239)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(10),Margin=new Thickness(0,0,10,0)};
        var preview=new Grid();preview.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});preview.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
        preview.Children.Add(new TextBlock{Text=LocalizationService.T("冻结画面","Frozen frame"),Foreground=new SolidColorBrush(Color.FromRgb(66,82,111)),FontSize=12,FontWeight=FontWeights.SemiBold,Margin=new Thickness(3,0,3,8)});
        var imageBorder=new Border{Background=new SolidColorBrush(Color.FromRgb(242,246,251)),CornerRadius=new CornerRadius(10),Padding=new Thickness(4),ClipToBounds=true};imageBorder.Child=_snapshotImage;Grid.SetRow(imageBorder,1);preview.Children.Add(imageBorder);previewShell.Child=preview;Grid.SetColumn(previewShell,0);body.Children.Add(previewShell);
        var conversationShell=new Border{Background=new SolidColorBrush(Color.FromRgb(242,246,251)),BorderBrush=new SolidColorBrush(Color.FromRgb(220,228,239)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(8)};
        _promptCanvas.Background=Brushes.Transparent;_promptCanvas.ClipToBounds=true;conversationShell.Child=_promptCanvas;Grid.SetColumn(conversationShell,1);body.Children.Add(conversationShell);
        Grid.SetRow(body,1);root.Children.Add(body);return root;
    }

    private static Button HeaderButton(string content,string tip)=>new(){Content=content,ToolTip=tip,Width=34,Height=30,Padding=new Thickness(0),Margin=new Thickness(4,0,0,0),FontSize=15,Foreground=new SolidColorBrush(Color.FromRgb(83,99,126)),Background=Brushes.Transparent,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=Cursors.Hand};

    internal void AttachPromptBar(FrameworkElement host)
    {
        _promptCanvas.Children.Add(host);LayoutPrompt();
    }

    internal bool HasPromptBar(FrameworkElement host)=>_promptCanvas.Children.Contains(host);

    private void LayoutPrompt()
    {
        if(!IsLoaded||_promptCanvas.ActualWidth<=0||_promptCanvas.ActualHeight<=0)return;
        _overlay.LayoutDetachedPrompt(_promptCanvas.ActualWidth,_promptCanvas.ActualHeight);
    }

    internal void MinimizeToWidget()
    {
        if(_closingForRedock||_closingForOwner)return;
        if(_widget is null){_widget=new ConversationFloatingWidget(this);_widget.Show();}
        Hide();
    }

    internal void RestoreFromWidget()
    {
        if(_closingForRedock||_closingForOwner)return;
        _widget?.CloseForOwnerExit();_widget=null;Show();WindowState=WindowState.Normal;Activate();LayoutPrompt();
    }

    internal void CloseSession()=>_overlay.CloseDetachedConversationWindow();

    internal void CloseForRedock()
    {
        _closingForRedock=true;_widget?.CloseForOwnerExit();_widget=null;Close();
    }

    internal void CloseForOwnerExit()
    {
        _closingForOwner=true;_widget?.CloseForOwnerExit();_widget=null;try{Close();}catch{}
    }

    private void OnClosed(object? sender,EventArgs e)
    {
        _widget?.CloseForOwnerExit();_widget=null;
        if(!_closingForRedock&&!_closingForOwner)_overlay.DetachedWorkspaceClosed(this);
    }
}
