// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using mewu_ai_Assistant.Services;

namespace mewu_ai_Assistant.Views;

public partial class CaptureOverlayWindow
{
    private bool _promptDetached,_promptDragging,_promptDockAnimating;
    private Point _promptDragOrigin;
    private Vector _promptDragOffset;
    private Rect _promptDragMonitor;
    private bool _promptDragWasDetached;
    private int _promptDockAnimationVersion;
    private ConversationWorkspaceWindow? _conversationWorkspaceWindow;

    private void PromptDragStarted(object sender,DragStartedEventArgs e)
    {
        var origin=new Point(Canvas.GetLeft(PromptBarHost),Canvas.GetTop(PromptBarHost));
        ++_promptDockAnimationVersion;_promptDockAnimating=false;
        PromptBarHost.BeginAnimation(Canvas.LeftProperty,null);PromptBarHost.BeginAnimation(Canvas.TopProperty,null);
        Canvas.SetLeft(PromptBarHost,origin.X);Canvas.SetTop(PromptBarHost,origin.Y);
        _promptDragMonitor=PromptMonitorBounds();_promptDragOrigin=origin;_promptDragOffset=new Vector();
        _promptDragWasDetached=_promptDetached;_promptDragging=true;
        UpdatePromptDockHint();
        SetPromptBarHidden(false,true);HideToolbarImmediately();e.Handled=true;
    }

    private void PromptDragDelta(object sender,DragDeltaEventArgs e)
    {
        if(!_promptDragging)return;
        // Thumb deltas are relative to its moving origin, including the resisted
        // distance. Reconstruct pointer travel rather than accumulating it twice.
        _promptDragOffset=new Vector(Canvas.GetLeft(PromptBarHost)-_promptDragOrigin.X+e.HorizontalChange,
            Canvas.GetTop(PromptBarHost)-_promptDragOrigin.Y+e.VerticalChange);
        if(!_promptDetached&&_promptDragOffset.Length>=64)_promptDetached=true;
        // A docked bar resists small accidental drags before detaching.
        var offset=_promptDetached?_promptDragOffset:_promptDragOffset*.25;
        var point=ClampFloatingPrompt(_promptDragOrigin+offset,_promptDragMonitor);
        Canvas.SetLeft(PromptBarHost,point.X);Canvas.SetTop(PromptBarHost,point.Y);UpdatePromptDockHint();e.Handled=true;
    }

    private void UpdatePromptDockHint()
    {
        var dock=CaptureOverlayPolicy.GetPromptBarBounds(_promptDragMonitor,PromptBar.DesiredSize.Height);
        if(dock.IsEmpty){PromptDockHint.Visibility=Visibility.Collapsed;return;}
        Canvas.SetLeft(PromptDockHint,dock.Left);Canvas.SetTop(PromptDockHint,dock.Top);
        PromptDockHint.Width=dock.Width;PromptDockHint.Height=dock.Height;
        var current=new Point(Canvas.GetLeft(PromptBarHost),Canvas.GetTop(PromptBarHost));
        PromptDockHint.Stroke=(current-dock.TopLeft).Length<=40?System.Windows.Media.Brushes.CornflowerBlue:System.Windows.Media.Brushes.LightSlateGray;
        PromptDockHint.Visibility=Visibility.Visible;
    }

    private Point ClampFloatingPrompt(Point point,Rect monitor)
    {
        var width=Math.Max(1,PromptBar.DesiredSize.Width);var height=Math.Max(1,PromptBar.DesiredSize.Height);
        return new Point(Math.Clamp(point.X,monitor.Left,Math.Max(monitor.Left,monitor.Right-width)),
            Math.Clamp(point.Y,monitor.Top,Math.Max(monitor.Top,monitor.Bottom-height)));
    }

    private void PromptDragCompleted(object sender,DragCompletedEventArgs e)
    {
        if(!_promptDragging)return;
        _promptDragging=false;
        PromptDockHint.Visibility=Visibility.Collapsed;
        if(e.Canceled)_promptDetached=_promptDragWasDetached;
        var dock=CaptureOverlayPolicy.GetPromptBarBounds(_promptDragMonitor,PromptBar.DesiredSize.Height);
        var current=new Point(Canvas.GetLeft(PromptBarHost),Canvas.GetTop(PromptBarHost));
        if(!_promptDetached||(current-dock.TopLeft).Length<=40)
        {
            _promptDetached=false;AnimatePromptDock(current,dock.TopLeft);
        }
        else if(e.Canceled)
        {
            Canvas.SetLeft(PromptBarHost,_promptDragOrigin.X);Canvas.SetTop(PromptBarHost,_promptDragOrigin.Y);
        }
        if(!e.Canceled&&_historyExpanded&&_promptDetached)
        {
            DetachConversationWindow();
            e.Handled=true;
            return;
        }
        e.Handled=true;
    }

    private void DetachConversationWindow()
    {
        if(_closed||_conversationWorkspaceWindow is not null||!_historyExpanded)return;
        ConversationWorkspaceWindow? workspace=null;
        try
        {
            var snapshot=RenderDetachedSnapshot();
            if(PromptBarHost.Parent is Panel parent)parent.Children.Remove(PromptBarHost);
            _promptDetached=true;_promptBarHidden=false;PromptBarHost.Visibility=Visibility.Visible;PromptBarHost.IsHitTestVisible=true;
            workspace=new ConversationWorkspaceWindow(this,snapshot);
            _conversationWorkspaceWindow=workspace;
            workspace.AttachPromptBar(PromptBarHost);
            Hide();Topmost=false;Cursor=Cursors.Arrow;
            _host.ReleaseCaptureForDetachedOverlay(this);
            workspace.Show();workspace.Activate();
        }
        catch(Exception ex)
        {
            new PrivacyLogger().Error("ConversationWorkspaceDetach",ex);
            try{workspace?.CloseForOwnerExit();}catch{}
            _conversationWorkspaceWindow=null;
            if(PromptBarHost.Parent is Panel parent)parent.Children.Remove(PromptBarHost);
            Root.Children.Add(PromptBarHost);
            PromptBarHost.Visibility=Visibility.Visible;PromptBarHost.IsHitTestVisible=true;_promptDetached=false;
            PromptStatus.Text=L("无法打开独立对话窗口，请重试。","Unable to open the conversation window. Please try again.");
            PositionPromptBar();
        }
    }

    private BitmapSource RenderDetachedSnapshot()
    {
        var hostVisibility=PromptBarHost.Visibility;var dimmerVisibility=Dimmer.Visibility;var toolbarVisibility=Toolbar.Visibility;var drawVisibility=DrawingToolbar.Visibility;var dockHintVisibility=PromptDockHint.Visibility;var sizeVisibility=SizeText.Visibility;var pointerVisibility=PointerInspector.Visibility;var countdownVisibility=RecordingCountdown.Visibility;
        try
        {
            PromptBarHost.Visibility=Visibility.Collapsed;Dimmer.Visibility=Visibility.Collapsed;Toolbar.Visibility=DrawingToolbar.Visibility=PromptDockHint.Visibility=SizeText.Visibility=PointerInspector.Visibility=RecordingCountdown.Visibility=Visibility.Collapsed;
            Root.UpdateLayout();
            var width=Math.Max(1,(int)Math.Round(Root.ActualWidth));var height=Math.Max(1,(int)Math.Round(Root.ActualHeight));
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(Root);bitmap.Freeze();return bitmap;
        }
        finally
        {
            PromptBarHost.Visibility=hostVisibility;Dimmer.Visibility=dimmerVisibility;Toolbar.Visibility=toolbarVisibility;DrawingToolbar.Visibility=drawVisibility;PromptDockHint.Visibility=dockHintVisibility;SizeText.Visibility=sizeVisibility;PointerInspector.Visibility=pointerVisibility;RecordingCountdown.Visibility=countdownVisibility;
        }
    }

    internal void LayoutDetachedPrompt(double width,double height)
    {
        if(_closed||_conversationWorkspaceWindow is null||width<=0||height<=0)return;
        var availableWidth=Math.Max(420,width-4);var availableHeight=Math.Max(300,height-4);
        PromptBar.Width=availableWidth;PromptBar.MaxHeight=availableHeight;PromptBarHost.Width=availableWidth;PromptBarHost.Height=double.NaN;
        HistoryPanel.MaxHeight=double.PositiveInfinity;HistoryScroll.MaxHeight=Math.Clamp(availableHeight*.22,84,Math.Max(84,availableHeight-180));ResponseScroll.MaxHeight=Math.Clamp(availableHeight*.42,120,Math.Max(120,availableHeight-150));AnswerScroll.MaxHeight=ResponseScroll.MaxHeight;
        PromptBar.Measure(new Size(availableWidth,availableHeight));PromptBarHost.Measure(new Size(availableWidth,availableHeight));
        PromptBarHost.Width=availableWidth;PromptBarHost.Height=Math.Min(availableHeight,Math.Max(PromptBar.DesiredSize.Height,PromptBarHost.DesiredSize.Height));Canvas.SetLeft(PromptBarHost,0);Canvas.SetTop(PromptBarHost,Math.Max(0,availableHeight-PromptBarHost.Height));
    }

    internal void RedockConversationWindow()
    {
        if(_conversationWorkspaceWindow is null||_closed)return;
        if(!_host.TryReacquireCaptureForOverlay(this)){_host.Notify(L("当前正在进行另一轮截图，请先完成后再吸附此会话。","Another screenshot is active. Finish it before docking this session back."));return;}
        var workspace=_conversationWorkspaceWindow;_conversationWorkspaceWindow=null;
        if(PromptBarHost.Parent is Panel parent)parent.Children.Remove(PromptBarHost);
        Root.Children.Add(PromptBarHost);workspace.CloseForRedock();
        _promptDetached=false;_promptBarHidden=false;PromptBarHost.Width=double.NaN;PromptBarHost.Height=double.NaN;Show();WindowState=WindowState.Normal;Topmost=true;Cursor=Cursors.Cross;Activate();PositionPromptBar();SetPromptBarHidden(false);QuickPrompt.Focus();
    }

    internal void RestoreFromConversationWidget()=>_conversationWorkspaceWindow?.RestoreFromWidget();

    internal void CloseDetachedConversationWindow()
    {
        var workspace=_conversationWorkspaceWindow;_conversationWorkspaceWindow=null;
        if(workspace is not null)workspace.CloseForOwnerExit();
        Close();
    }

    internal void DetachedWorkspaceClosed(ConversationWorkspaceWindow workspace)
    {
        if(!ReferenceEquals(_conversationWorkspaceWindow,workspace))return;
        _conversationWorkspaceWindow=null;Close();
    }

    private void AnimatePromptDock(Point from,Point to)
    {
        var version=++_promptDockAnimationVersion;
        Canvas.SetLeft(PromptBarHost,to.X);Canvas.SetTop(PromptBarHost,to.Y);
        if(!SystemParameters.ClientAreaAnimation){PositionPromptBar();return;}
        _promptDockAnimating=true;
        var ease=new ElasticEase{EasingMode=EasingMode.EaseOut,Oscillations=2,Springiness=5};
        var x=new DoubleAnimation(from.X,to.X,TimeSpan.FromMilliseconds(460)){EasingFunction=ease,FillBehavior=FillBehavior.Stop};
        var y=new DoubleAnimation(from.Y,to.Y,TimeSpan.FromMilliseconds(460)){EasingFunction=ease,FillBehavior=FillBehavior.Stop};
        y.Completed+=(_,_)=>{if(_closed||version!=_promptDockAnimationVersion)return;_promptDockAnimating=false;PositionPromptBar();};
        PromptBarHost.BeginAnimation(Canvas.LeftProperty,x);PromptBarHost.BeginAnimation(Canvas.TopProperty,y);
    }
}
