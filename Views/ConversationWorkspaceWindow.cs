// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using mewu_ai_Assistant.Services;

namespace mewu_ai_Assistant.Views;

/// <summary>Detached form of the original conversation bar over the live capture canvas.</summary>
internal sealed class ConversationWorkspaceWindow : Window
{
    private readonly CaptureOverlayWindow _overlay;
    private readonly Canvas _promptCanvas=new();
    private readonly Border _surface;
    private ConversationFloatingWidget? _widget;
    private bool _closingForRedock,_closingForOwner,_dragging;
    private int _resizeHandleCount;
    private Point _dragOrigin;
    private double _dragLeft,_dragTop;

    internal ConversationWorkspaceWindow(CaptureOverlayWindow overlay,Rect initialBounds)
    {
        _overlay=overlay??throw new ArgumentNullException(nameof(overlay));
        Title=LocalizationService.T("截图会话","Screenshot conversation");
        Width=Math.Clamp(initialBounds.Width>0?initialBounds.Width:680,520,980);
        Height=Math.Clamp(initialBounds.Height>0?initialBounds.Height:430,300,760);
        MinWidth=480;MinHeight=280;Left=initialBounds.Left;Top=initialBounds.Top;
        WindowStartupLocation=WindowStartupLocation.Manual;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;
        Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=true;Topmost=true;
        _surface=new Border{Background=new SolidColorBrush(Color.FromRgb(249,251,255)),BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(18)};
        Content=BuildContent();SizeChanged+=(_,_)=>LayoutPrompt();Loaded+=(_,_)=>{ClampToWorkArea();LayoutPrompt();};Closed+=OnClosed;
    }

    private FrameworkElement BuildContent()
    {
        var outer=new Grid{Margin=new Thickness(10)};
        var body=new Grid{Margin=new Thickness(2)};body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
        var header=new Border{Background=new SolidColorBrush(Color.FromRgb(249,251,255)),Padding=new Thickness(12,8,10,8),CornerRadius=new CornerRadius(16,16,0,0)};header.MouseLeftButtonDown+=BeginMove;
        var titleRow=new DockPanel{LastChildFill=true};
        var close=HeaderButton(LocalizationService.T("关闭会话","Close conversation"),"M4,4 L12,12 M12,4 L4,12");close.Click+=(_,_)=>CloseSession();DockPanel.SetDock(close,Dock.Right);titleRow.Children.Add(close);
        var minimize=HeaderButton(LocalizationService.T("最小化为悬浮窗","Minimize to floating widget"),"M3,8 L13,8");minimize.Click+=(_,_)=>MinimizeToWidget();DockPanel.SetDock(minimize,Dock.Right);titleRow.Children.Add(minimize);
        var redock=HeaderButton(LocalizationService.T("吸附回原截图对话条","Dock back to the original conversation bar"),"M3,11 L3,5 L11,5 M8,2 L11,5 L8,8");redock.Margin=new Thickness(0,0,6,0);redock.Click+=(_,_)=>_overlay.RedockConversationWindow();DockPanel.SetDock(redock,Dock.Right);titleRow.Children.Add(redock);
        var heading=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        var icon=new Border{Width=28,Height=28,CornerRadius=new CornerRadius(9),Background=new SolidColorBrush(Color.FromRgb(232,237,255)),Margin=new Thickness(0,0,8,0)};icon.Child=new System.Windows.Shapes.Path{Width=15,Height=15,Stretch=Stretch.Uniform,Stroke=new SolidColorBrush(Color.FromRgb(82,99,217)),StrokeThickness=1.6,Data=Geometry.Parse("M2,2 L14,2 L14,11 L8,11 L4,14 L4,11 L2,11 Z M5,5 L11,5 M5,8 L9,8"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};heading.Children.Add(icon);
        var labels=new StackPanel();labels.Children.Add(new TextBlock{Text=LocalizationService.T("截图会话","Screenshot conversation"),Foreground=new SolidColorBrush(Color.FromRgb(40,53,78)),FontSize=13.5,FontWeight=FontWeights.SemiBold});labels.Children.Add(new TextBlock{Text=LocalizationService.T("原画面已冻结 · 对话和标注可继续操作","Original frame frozen · chat and annotations stay available"),Foreground=new SolidColorBrush(Color.FromRgb(111,126,151)),FontSize=10.5,Margin=new Thickness(0,1,0,0)});heading.Children.Add(labels);titleRow.Children.Add(heading);header.Child=titleRow;body.Children.Add(header);
        _promptCanvas.Background=Brushes.Transparent;_promptCanvas.ClipToBounds=true;_promptCanvas.Margin=new Thickness(8,0,8,8);Grid.SetRow(_promptCanvas,1);body.Children.Add(_promptCanvas);
        _surface.Child=body;outer.Children.Add(_surface);AddResizeHandles(outer);return outer;
    }

    private static Button HeaderButton(string tip,string pathData)
    {
        var button=new Button{ToolTip=tip,Width=30,Height=28,Padding=new Thickness(0),Margin=new Thickness(3,0,0,0),Foreground=new SolidColorBrush(Color.FromRgb(76,91,119)),Background=Brushes.Transparent,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Focusable=false};
        button.Content=new System.Windows.Shapes.Path{Data=Geometry.Parse(pathData),Width=14,Height=14,Stretch=Stretch.Uniform,Stroke=button.Foreground,StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        return button;
    }
    private void AddResizeHandles(Grid root){AddResizeHandle(root,"N",HorizontalAlignment.Stretch,VerticalAlignment.Top,new Thickness(18,2,18,0),Cursors.SizeNS);AddResizeHandle(root,"S",HorizontalAlignment.Stretch,VerticalAlignment.Bottom,new Thickness(18,0,18,2),Cursors.SizeNS);AddResizeHandle(root,"W",HorizontalAlignment.Left,VerticalAlignment.Stretch,new Thickness(2,18,0,18),Cursors.SizeWE);AddResizeHandle(root,"E",HorizontalAlignment.Right,VerticalAlignment.Stretch,new Thickness(0,18,2,18),Cursors.SizeWE);AddResizeHandle(root,"NW",HorizontalAlignment.Left,VerticalAlignment.Top,new Thickness(2),Cursors.SizeNWSE);AddResizeHandle(root,"SE",HorizontalAlignment.Right,VerticalAlignment.Bottom,new Thickness(2),Cursors.SizeNWSE);AddResizeHandle(root,"NE",HorizontalAlignment.Right,VerticalAlignment.Top,new Thickness(2),Cursors.SizeNESW);AddResizeHandle(root,"SW",HorizontalAlignment.Left,VerticalAlignment.Bottom,new Thickness(2),Cursors.SizeNESW);}
    private void AddResizeHandle(Grid root,string edge,HorizontalAlignment horizontal,VerticalAlignment vertical,Thickness margin,Cursor cursor){var thumb=new Thumb{Tag=edge,Background=Brushes.Transparent,HorizontalAlignment=horizontal,VerticalAlignment=vertical,Margin=margin,Cursor=cursor};thumb.DragDelta+=ResizeDelta;root.Children.Add(thumb);_resizeHandleCount++;}
    private void BeginMove(object? sender,MouseButtonEventArgs e){if(e.ChangedButton!=MouseButton.Left||e.OriginalSource is Button)return;_dragging=true;_dragOrigin=PointToScreen(e.GetPosition(this));_dragLeft=Left;_dragTop=Top;Mouse.Capture((UIElement)sender!);e.Handled=true;}
    private void ResizeDelta(object sender,DragDeltaEventArgs e){if(sender is not Thumb thumb)return;var edge=thumb.Tag?.ToString()??"";var left=Left;var top=Top;var width=Width;var height=Height;if(edge.Contains('E'))width=Math.Max(MinWidth,width+e.HorizontalChange);if(edge.Contains('S'))height=Math.Max(MinHeight,height+e.VerticalChange);if(edge.Contains('W')){var next=Math.Max(MinWidth,width-e.HorizontalChange);left+=width-next;width=next;}if(edge.Contains('N')){var next=Math.Max(MinHeight,height-e.VerticalChange);top+=height-next;height=next;}Width=width;Height=height;Left=left;Top=top;ClampToWorkArea();LayoutPrompt();e.Handled=true;}
    protected override void OnMouseMove(MouseEventArgs e){if(_dragging){var p=PointToScreen(e.GetPosition(this));Left=_dragLeft+p.X-_dragOrigin.X;Top=_dragTop+p.Y-_dragOrigin.Y;ClampToWorkArea();LayoutPrompt();}base.OnMouseMove(e);}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){if(_dragging){_dragging=false;Mouse.Capture(null);e.Handled=true;}base.OnMouseLeftButtonUp(e);}
    private void ClampToWorkArea(){var area=SystemParameters.WorkArea;Left=Math.Clamp(Left,area.Left,Math.Max(area.Left,area.Right-Width));Top=Math.Clamp(Top,area.Top,Math.Max(area.Top,area.Bottom-Height));}

    internal CaptureOverlayWindow Overlay=>_overlay;
    internal bool HasResizeHandles=>_resizeHandleCount==8;
    internal void AttachPromptBar(FrameworkElement host){_promptCanvas.Children.Add(host);LayoutPrompt();}
    internal bool HasPromptBar(FrameworkElement host)=>_promptCanvas.Children.Contains(host);
    private void LayoutPrompt(){if(!IsLoaded||_promptCanvas.ActualWidth<=0||_promptCanvas.ActualHeight<=0)return;_overlay.LayoutDetachedPrompt(_promptCanvas.ActualWidth,_promptCanvas.ActualHeight);}
    internal void MinimizeToWidget(){if(_closingForRedock||_closingForOwner)return;if(_widget is null){_widget=new ConversationFloatingWidget(this);_widget.Show();}_overlay.HideDetachedSession();Hide();}
    internal void RestoreFromWidget(){if(_closingForRedock||_closingForOwner)return;_widget?.CloseForOwnerExit();_widget=null;_overlay.ShowDetachedSession();Show();WindowState=WindowState.Normal;Activate();LayoutPrompt();}
    internal void CloseSession()=>_overlay.CloseDetachedConversationWindow();
    internal void CloseForRedock(){_closingForRedock=true;_widget?.CloseForOwnerExit();_widget=null;Close();}
    internal void CloseForOwnerExit(){_closingForOwner=true;_widget?.CloseForOwnerExit();_widget=null;try{Close();}catch{}}
    private void OnClosed(object? sender,EventArgs e){_widget?.CloseForOwnerExit();_widget=null;if(!_closingForRedock&&!_closingForOwner)_overlay.DetachedWorkspaceClosed(this);}
}
