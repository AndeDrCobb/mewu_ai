// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Diagnostics;
using mewu_ai_Assistant.Interop;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;

namespace mewu_ai_Assistant.Views;

public partial class CaptureOverlayWindow
{
    private ApplicationSnapshotTarget? _pendingSnapshotTarget;
    private bool _applicationSnapshotActive;
    private bool _closeAfterApplicationSnapshot;
    private string? _applicationSnapshotFailure;

    private ApplicationSnapshotTarget? ResolveSnapshotTarget(Point point)
    {
        var pixel=ScreenCoordinateService.ToScreenPixelPoint(point,Root.ActualWidth,Root.ActualHeight,_frame.Image.PixelWidth,_frame.Image.PixelHeight,_frame.OriginX,_frame.OriginY);
        var target=_windowSnap.FindFastTargetAt(pixel.X,pixel.Y,new WindowInteropHelper(this).Handle);
        return target is null?null:ApplicationSnapshotTarget.FromWindow(target.Handle);
    }

    private void UpdateApplicationSnapshotTool(SelectionItem item)
    {
        var label=item.SnapshotTarget is null
            ?L("滚动长截图 · F8 完成，Esc 取消","Scrolling capture · F8 to finish, Esc to cancel")
            :L("截取应用快照 · 自动获取完整画面","Capture application snapshot · Automatically capture full content");
        LongCaptureButton.ToolTip=label;AutomationProperties.SetName(LongCaptureButton,label);
        if(LongCaptureButton.Content is System.Windows.Shapes.Path icon)
            icon.Data=Geometry.Parse(item.SnapshotTarget is null
                ?"M5,2 L13,2 L13,16 L5,16 Z M3,5 L5,3 L7,5 M11,13 L13,15 L15,13"
                :"M2,2 L16,2 L16,16 L2,16 Z M2,6 L16,6 M5,9 L13,9 M5,12 L13,12");
    }

    private async Task CaptureApplicationSnapshotAsync(SelectionItem item)
    {
        if(item.SnapshotTarget is not { } target)return;
        var operation=BeginOverlayOperation(L("正在截取完整应用画面…按 Esc 可取消","Capturing full application content… Press Esc to cancel"));
        ApplicationScrollSession? scroll=null;
        ApplicationWindowCapture? capture=null;
        PinnedImageWindow? pin=null;
        var interop=new WindowInteropHelper(this);var previousOwner=interop.Owner;var ownerChanged=false;
        var restored=false;var stage="prepare";_applicationSnapshotActive=true;_applicationSnapshotFailure=null;
        try
        {
            scroll=await ApplicationScrollSession.StartAsync(target,operation.Token);
            capture=new ApplicationWindowCapture(target);
            var viewport=scroll.Initial.Scrollable?scroll.Initial.Bounds.Intersect(capture.Bounds):capture.Bounds;
            if(viewport.IsEmpty)throw new InvalidDataException("Scroll viewport is outside the source surface.");
            // The existing frozen overlay stays opaque throughout capture. We
            // read only the source HWND, so teaching visibility stays unchanged.
            var handle=interop.Handle;
            interop.Owner=new IntPtr(target.Handle);ownerChanged=true;
            if(NativeMethods.GetWindow(handle,4)!=new IntPtr(target.Handle))throw new InvalidOperationException("Frozen foreground ownership was not applied.");
            if(!NativeMethods.SetWindowPos(handle,new IntPtr(-1),0,0,0,0,0x0001|0x0002))throw new InvalidOperationException("Cannot retain frozen foreground.");
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(operation.Token);deadline.CancelAfter(TimeSpan.FromMinutes(4));
            var token=deadline.Token;
            var generation=capture.Generation;
            stage="top";var state=scroll.Initial.Scrollable?await scroll.MoveAsync(0,token):scroll.Initial;
            stage="first-frame";var first=await ReadApplicationFrameAsync(capture,viewport,generation,token);
            var accumulator=ScrollingCaptureAccumulator.Start(first);
            while(state.Scrollable&&state.Position<99.99)
            {
                token.ThrowIfCancellationRequested();
                if(!IsOverlayOperationActive(operation,item)||!target.IsCurrent())throw new OperationCanceledException(token);
                if(state.ViewSize>=100)throw new InvalidDataException("Invalid scroll range.");
                var step=Math.Clamp(state.ViewSize/(100-state.ViewSize)*45,.001,100);
                generation=capture.Generation;
                stage="move";var next=await scroll.MoveAsync(Math.Min(100,state.Position+step),token);
                if(next.Bounds!=state.Bounds||Math.Abs(next.ViewSize-state.ViewSize)>.01||next.Position<=state.Position)
                    throw new InvalidDataException("Source content changed while capturing.");
                stage="frame";var frame=await ReadApplicationFrameAsync(capture,viewport,generation,token);
                var expected=viewport.Height*(100-state.ViewSize)/state.ViewSize*(next.Position-state.Position)/100;
                var shift=await Task.Run(()=>ScrollingCaptureComposer.EstimateVerticalShift(accumulator.LastFrame,frame,out _,null,1,expected),token);
                stage="stitch";if(shift<=0)throw new InvalidDataException("Content overlap could not be verified.");
                accumulator=await Task.Run(()=>accumulator.Append(frame,shift,token),token)??throw new InvalidDataException("Image capacity reached.");
                state=next;
            }
            stage="restore";await scroll.RestoreAsync();restored=true;
            await Task.Delay(200,token);
            if(!IsOverlayOperationActive(operation,item))return;
            // End the compositor session before refreshing the desktop, so a
            // system capture border cannot become part of the frozen result.
            capture.Dispose();capture=null;
            interop.Owner=previousOwner;ownerChanged=false;
            NativeMethods.FlushComposition();
            var image=accumulator.Composite;var before=CaptureOverlaySnapshot();
            var bounds=CaptureOverlayPolicy.FitLongCaptureResultBounds(item.Bounds,MonitorBounds(item.Bounds),image.PixelWidth,image.PixelHeight);
            var region=ScreenCoordinateService.ToScreenRect(ToPixelRect(bounds),_frame.OriginX,_frame.OriginY);
            stage="pin";pin=new PinnedImageWindow(image,region,IsTeachingMode);pin.Show();
            ClearImageOnlyLayers(item);item.CapturedImageOverride=image;item.SnapshotText=null;item.SnapshotTarget=null;item.Bounds=bounds;
            _references.Add(item);UpdateSelection(item);UpdateReferenceChips();
            RecordOverlayOperation(before,L("应用快照","Application snapshot"));
            RefreshDesktopFrameIncludingPinnedWindows();RestoreOverlayKeyboardFocusAfterPin();KeepOverlayBelowPinnedWindows();SetPromptBarHidden(false);
            PromptStatus.Text=scroll.Initial.Scrollable
                ?L("完整画面已置顶并引用 · 原窗口位置已恢复","Full content pinned and referenced · Original position restored")
                :L("窗口画面已置顶并引用 · 未检测到可自动展开的滚动区域","Window pinned and referenced · No accessible scroll area detected");
            pin=null;
        }
        catch(OperationCanceledException){}
        catch(Exception ex)
        {
            _applicationSnapshotFailure=stage+":"+ex.GetType().Name;
            new PrivacyLogger().Info("ApplicationSnapshotFailed",_applicationSnapshotFailure);
            if(!_closed)PromptStatus.Text=L("未能取得完整画面：此窗口的渲染或滚动接口不可用，或内容无法可靠拼接。","Full capture unavailable: the window could not provide rendering, scroll control, or reliable image overlap.");
        }
        finally
        {
            if(scroll is not null)
            {
                try{if(!restored){await scroll.RestoreAsync();restored=true;}}
                catch(Exception){if(!_closed)PromptStatus.Text=L("快照已停止，应用未确认恢复原位置，请检查原窗口。","Snapshot stopped. The application did not confirm restoration; check its scroll position.");}
                try{await scroll.DisposeAsync();}catch(Exception ex){new PrivacyLogger().Info("ApplicationSnapshotCleanup",ex.GetType().Name);}
            }
            try{capture?.Dispose();}catch(Exception ex){new PrivacyLogger().Info("ApplicationWindowCaptureCleanup",ex.GetType().Name);}
            pin?.Close();
            if(ownerChanged&&!_closed)
            {
                try{interop.Owner=previousOwner;}
                catch(Exception ex){new PrivacyLogger().Info("ApplicationSnapshotOwnerRestore",ex.GetType().Name);_closeAfterApplicationSnapshot=true;}
            }
            if(operation.IsCancellationRequested&&restored&&!_closed)PromptStatus.Text=L("已取消快照 · 原位置已恢复","Snapshot cancelled · Original position restored");
            _applicationSnapshotActive=false;EndOverlayOperation(operation);
            if(_closeAfterApplicationSnapshot&&!_closed){_closeAfterApplicationSnapshot=false;Close();}
        }
    }

    private static async Task<BitmapSource> ReadApplicationFrameAsync(ApplicationWindowCapture capture,ScreenRect viewport,long previousGeneration,CancellationToken token)
    {
        var timer=Stopwatch.StartNew();await Task.Delay(160,token);
        BitmapSource? previous=null;
        while(timer.Elapsed<TimeSpan.FromSeconds(5))
        {
            token.ThrowIfCancellationRequested();
            if(capture.Generation>previousGeneration&&capture.Latest is { } image)
            {
                var crop=new CroppedBitmap(image,new Int32Rect(viewport.X-capture.Bounds.X,viewport.Y-capture.Bounds.Y,viewport.Width,viewport.Height));crop.Freeze();
                if(previous is not null&&await Task.Run(()=>ApplicationFramesEqual(previous,crop),token))return crop;
                previous=crop;
            }
            await Task.Delay(100,token);
        }
        throw new TimeoutException("Window did not deliver a stable updated frame.");
    }

    private static bool ApplicationFramesEqual(BitmapSource first,BitmapSource second)
    {
        var stride=checked(first.PixelWidth*4);var a=new byte[checked(stride*first.PixelHeight)];var b=new byte[a.Length];
        try{first.CopyPixels(a,stride,0);second.CopyPixels(b,stride,0);return a.AsSpan().SequenceEqual(b);}
        finally{Array.Clear(a);Array.Clear(b);}
    }

    private void ApplicationSnapshotClosing(object? sender,System.ComponentModel.CancelEventArgs e)
    {
        if(!_applicationSnapshotActive)return;
        e.Cancel=true;_closeAfterApplicationSnapshot=true;TryCancel(_overlayRequest);
    }
}
