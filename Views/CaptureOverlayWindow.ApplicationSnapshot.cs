// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using mewu_ai_Assistant.Services;

namespace mewu_ai_Assistant.Views;

public partial class CaptureOverlayWindow
{
    private ApplicationSnapshotTarget? _pendingSnapshotTarget;

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
            :L("截取应用快照 · 包括应用提供的屏幕外文本","Capture application snapshot · Include available off-screen text");
        LongCaptureButton.ToolTip=label;AutomationProperties.SetName(LongCaptureButton,label);
        if(LongCaptureButton.Content is System.Windows.Shapes.Path icon)
            icon.Data=Geometry.Parse(item.SnapshotTarget is null
                ?"M5,2 L13,2 L13,16 L5,16 Z M3,5 L5,3 L7,5 M11,13 L13,15 L15,13"
                :"M2,2 L16,2 L16,16 L2,16 Z M2,6 L16,6 M5,9 L13,9 M5,12 L13,12");
    }

    private async Task CaptureApplicationSnapshotAsync(SelectionItem item)
    {
        if(item.SnapshotTarget is not { } target)return;
        var operation=BeginOverlayOperation(L("正在截取应用快照…按 Esc 可取消","Capturing application snapshot… Press Esc to cancel"));
        PinnedImageWindow? pin=null;
        var reading=true;
        try
        {
            var document=await ApplicationSnapshotProcess.ReadAsync(target,operation.Token);
            reading=false;
            if(!IsOverlayOperationActive(operation,item))return;
            var pixels=ToPixelRect(item.Bounds);
            var english=System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="en";
            var image=await Task.Run(()=>ApplicationSnapshotRenderer.Render(document,pixels.Width,english,operation.Token),operation.Token);
            if(!IsOverlayOperationActive(operation,item))return;
            var before=CaptureOverlaySnapshot();
            var bounds=CaptureOverlayPolicy.FitLongCaptureResultBounds(item.Bounds,MonitorBounds(item.Bounds),image.PixelWidth,image.PixelHeight);
            var region=ScreenCoordinateService.ToScreenRect(ToPixelRect(bounds),_frame.OriginX,_frame.OriginY);
            pin=new PinnedImageWindow(image,region,IsTeachingMode);pin.Show();
            if(!IsOverlayOperationActive(operation,item))return;
            ClearImageOnlyLayers(item);item.CapturedImageOverride=image;item.SnapshotText=document.Text;item.SnapshotTarget=null;item.Bounds=bounds;
            _references.Add(item);UpdateSelection(item);UpdateReferenceChips();
            RecordOverlayOperation(before,L("应用快照","Application snapshot"));
            RefreshDesktopFrameIncludingPinnedWindows();RestoreOverlayKeyboardFocusAfterPin();KeepOverlayBelowPinnedWindows();SetPromptBarHidden(false);
            PromptStatus.Text=L("应用内容快照已置顶并引用 · 未加载的内容可能无法读取","Content snapshot pinned and referenced · Unloaded content may be unavailable");
            pin=null;
        }
        catch(OperationCanceledException){}
        catch(Exception ex)
        {
            // Cross-process provider errors must never log the application text.
            new PrivacyLogger().Info("ApplicationSnapshotFailed",ex.GetType().Name);
            if(IsOverlayOperationActive(operation,item))PromptStatus.Text=ex is TimeoutException||!reading&&ex is InvalidDataException?ex.Message:L("应用快照失败：窗口可能已关闭、未响应或未提供文本；可拖选区域使用长截图。","Snapshot failed: the window may be closed, unresponsive or lack readable text. Drag a region for scrolling capture.");
        }
        finally{pin?.Close();EndOverlayOperation(operation);}
    }
}
