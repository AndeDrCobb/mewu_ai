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
        try
        {
            ApplicationSnapshotDocument? document=null;
            try{document=await ApplicationSnapshotProcess.ReadAsync(target,operation.Token);}
            catch(Exception ex) when(ex is InvalidDataException or IOException or TimeoutException)
            {
                // Text is optional context. A provider without readable text
                // must not prevent the original window image from being used.
                new PrivacyLogger().Info("ApplicationSnapshotTextUnavailable",ex.GetType().Name);
            }
            if(!IsOverlayOperationActive(operation,item))return;
            var image=RenderSelectionImage(item,false,false,false);
            var before=CaptureOverlaySnapshot();
            var bounds=item.Bounds;
            var region=ScreenCoordinateService.ToScreenRect(ToPixelRect(bounds),_frame.OriginX,_frame.OriginY);
            pin=new PinnedImageWindow(RenderSelectionImage(item,true,true,true),region,IsTeachingMode);pin.Show();
            if(!IsOverlayOperationActive(operation,item))return;
            item.CapturedImageOverride=image;item.SnapshotText=document?.Text;item.SnapshotTarget=null;
            _references.Add(item);UpdateSelection(item);UpdateReferenceChips();
            RecordOverlayOperation(before,L("应用快照","Application snapshot"));
            RefreshDesktopFrameIncludingPinnedWindows();RestoreOverlayKeyboardFocusAfterPin();KeepOverlayBelowPinnedWindows();SetPromptBarHidden(false);
            PromptStatus.Text=document is null
                ?L("窗口原图已置顶并引用 · 此应用未提供额外文本","Original window pinned and referenced · No additional text available")
                :L("窗口原图已置顶并引用 · 已附带应用提供的文本","Original window pinned and referenced · Available application text attached");
            pin=null;
        }
        catch(OperationCanceledException){}
        catch(Exception ex)
        {
            // Cross-process provider errors must never log the application text.
            new PrivacyLogger().Info("ApplicationSnapshotFailed",ex.GetType().Name);
            if(IsOverlayOperationActive(operation,item))PromptStatus.Text=L("应用快照失败：窗口可能已关闭或改变，请重新选择。","Snapshot failed: the source window may have closed or changed. Select it again.");
        }
        finally{pin?.Close();EndOverlayOperation(operation);}
    }
}
