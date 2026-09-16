// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        e.Handled=true;
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
