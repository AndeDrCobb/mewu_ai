// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
    private readonly List<(DependencyObject Target,DependencyProperty Property,object Value)> _conversationLayoutRestore=[];
    private StackPanel? _conversationHistoryStack;
    private Grid? _conversationHistoryGrid;

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
            var localLeft=Canvas.GetLeft(PromptBarHost);var localTop=Canvas.GetTop(PromptBarHost);
            var screenOrigin=PresentationSource.FromVisual(this) is not null
                ? PointToScreen(new Point(Math.Max(0,localLeft),Math.Max(0,localTop)))
                : new Point(Left+Math.Max(0,localLeft),Top+Math.Max(0,localTop));
            var initialWidth=Math.Max(520,Math.Min(900,Math.Max(PromptBar.ActualWidth,PromptBar.DesiredSize.Width)));
            var initialHeight=Math.Max(300,Math.Min(720,Math.Max(PromptBar.ActualHeight,PromptBar.DesiredSize.Height)));
            _promptDetached=true;_promptBarHidden=false;
            workspace=new ConversationWorkspaceWindow(this,new Rect(screenOrigin.X,screenOrigin.Y,initialWidth,initialHeight));
            _conversationWorkspaceWindow=workspace;
            PrepareConversationLayout();
            workspace.AttachConversation(PromptContent);
            // The capture overlay remains the frozen screenshot canvas.  Only
            // the original conversation bar is lifted into the compact panel.
            Show();Topmost=true;Cursor=Cursors.Arrow;
            _host.ReleaseCaptureForDetachedOverlay(this);
            workspace.Show();workspace.Activate();
        }
        catch(Exception ex)
        {
            new PrivacyLogger().Error("ConversationWorkspaceDetach",ex);
            try{workspace?.CloseForOwnerExit();}catch{}
            _conversationWorkspaceWindow=null;
            RestoreConversationLayout();
            PromptBarHost.Visibility=Visibility.Visible;PromptBarHost.IsHitTestVisible=true;_promptDetached=false;
            PromptStatus.Text=L("无法打开独立对话窗口，请重试。","Unable to open the conversation window. Please try again.");
            PositionPromptBar();
        }
    }

    internal void LayoutDetachedPrompt(double width,double height)
    {
        if(_closed||_conversationWorkspaceWindow is null||width<=0||height<=0)return;
        // The content grid stretches with the window. History/answer scroll in
        // the available space; the composer and status stay at the bottom.
        var hasAnswer=ResponseScroll.Visibility==Visibility.Visible;
        PromptContent.RowDefinitions[0].Height=new GridLength(hasAnswer?.4:1,GridUnitType.Star);
        PromptContent.RowDefinitions[1].Height=hasAnswer?new GridLength(.6,GridUnitType.Star):GridLength.Auto;
        var answerHeight=Math.Max(60,(height-150)*.6-50);
        AnswerScroll.MaxHeight=answerHeight;
    }

    private void SetConversationProperty(DependencyObject target,DependencyProperty property,object value)
    {
        _conversationLayoutRestore.Add((target,property,target.ReadLocalValue(property)));
        target.SetValue(property,value);
    }

    private void PrepareConversationLayout()
    {
        PromptBar.Child=null;
        SetConversationProperty(PromptBarHost,VisibilityProperty,Visibility.Collapsed);
        SetConversationProperty(PromptContent,MarginProperty,new Thickness(0));
        SetConversationProperty(HistorySection,VisibilityProperty,Visibility.Collapsed);
        HistorySection.Children.Remove(HistoryPanel);
        PromptContent.Children.Add(HistoryPanel);
        SetConversationProperty(HistoryPanel,Grid.RowProperty,0);
        SetConversationProperty(HistoryPanel,Border.BackgroundProperty,Brushes.Transparent);
        SetConversationProperty(HistoryPanel,Border.BorderThicknessProperty,new Thickness(0));
        SetConversationProperty(HistoryPanel,Border.PaddingProperty,new Thickness(0));
        SetConversationProperty(HistoryPanel,MarginProperty,new Thickness(2,0,2,12));
        SetConversationProperty(HistoryPanel,MaxHeightProperty,double.PositiveInfinity);
        SetConversationProperty(HistoryScroll,MaxHeightProperty,double.PositiveInfinity);
        SetConversationProperty(ResponseScroll,MaxHeightProperty,double.PositiveInfinity);
        SetConversationProperty(AnswerScroll,MaxHeightProperty,double.PositiveInfinity);
        foreach(var row in PromptContent.RowDefinitions.Take(2))
            SetConversationProperty(row,RowDefinition.HeightProperty,new GridLength(1,GridUnitType.Star));
        _conversationHistoryStack=(StackPanel)HistoryPanel.Child;
        HistoryPanel.Child=null;
        _conversationHistoryGrid=new Grid();
        for(var i=0;i<_conversationHistoryStack.Children.Count;i++)
            _conversationHistoryGrid.RowDefinitions.Add(new RowDefinition{Height=i==2?new GridLength(1,GridUnitType.Star):GridLength.Auto});
        var children=_conversationHistoryStack.Children.Cast<UIElement>().ToArray();
        _conversationHistoryStack.Children.Clear();
        for(var i=0;i<children.Length;i++){SetConversationProperty(children[i],Grid.RowProperty,i);_conversationHistoryGrid.Children.Add(children[i]);}
        HistoryPanel.Child=_conversationHistoryGrid;
    }

    private void RestoreConversationLayout()
    {
        if(_conversationLayoutRestore.Count==0)return;
        if(PromptContent.Parent is Panel parent)parent.Children.Remove(PromptContent);
        PromptContent.Children.Remove(HistoryPanel);
        HistorySection.Children.Add(HistoryPanel);
        if(_conversationHistoryGrid is not null&&_conversationHistoryStack is not null)
        {
            var children=_conversationHistoryGrid.Children.Cast<UIElement>().ToArray();
            _conversationHistoryGrid.Children.Clear();HistoryPanel.Child=null;
            foreach(var child in children)_conversationHistoryStack.Children.Add(child);
            HistoryPanel.Child=_conversationHistoryStack;
        }
        foreach(var (target,property,value) in _conversationLayoutRestore)
            if(value==DependencyProperty.UnsetValue)target.ClearValue(property);else target.SetValue(property,value);
        _conversationLayoutRestore.Clear();_conversationHistoryGrid=null;_conversationHistoryStack=null;
        PromptBar.Child=PromptContent;
    }

    internal void HandleConversationKeyDown(KeyEventArgs e)
    {
        if(e.Key==Key.Escape){HandleEscape();e.Handled=true;}
        else if(e.Key==Key.F8&&_teachingCaptureFinishRegistered){FinishTeachingLiveCapture();e.Handled=true;}
    }

    internal void RedockConversationWindow()
    {
        if(_conversationWorkspaceWindow is null||_closed)return;
        if(!_host.TryReacquireCaptureForOverlay(this)){_host.Notify(L("当前正在进行另一轮截图，请先完成后再吸附此会话。","Another screenshot is active. Finish it before docking this session back."));return;}
        var workspace=_conversationWorkspaceWindow;_conversationWorkspaceWindow=null;
        RestoreConversationLayout();workspace.CloseForRedock();
        _promptDetached=false;_promptBarHidden=false;PromptBarHost.Width=double.NaN;PromptBarHost.Height=double.NaN;Show();WindowState=WindowState.Normal;Topmost=true;Cursor=Cursors.Cross;Activate();PositionPromptBar();SetPromptBarHidden(false);QuickPrompt.Focus();
    }

    internal void RestoreFromConversationWidget()=>_conversationWorkspaceWindow?.RestoreFromWidget();

    internal void HideDetachedSession()
    {
        if(_conversationWorkspaceWindow is null||_closed)return;
        Hide();
    }

    internal void ShowDetachedSession()
    {
        if(_conversationWorkspaceWindow is null||_closed)return;
        Show();Topmost=true;
    }

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
