// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using mewu_ai_Assistant.Interop;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using mewu_ai_Assistant.Views;
using Application=System.Windows.Application;

// Explicit opt-in for an already selected real HWND. Never saves screen pixels,
// document text, titles, configuration or credentials; never sends an AI request.
internal static class ApplicationSnapshotLiveReplay
{
    internal static void Run(Application app,AppHost host,long handle)
    {
        var target=ApplicationSnapshotTarget.FromWindow(new IntPtr(handle))??throw new InvalidOperationException("Selected window is unavailable.");
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var overlay=new CaptureOverlayWindow(host);Program.MarkReplayWindow(overlay,"应用快照检查 · 原窗口 · 不发送 AI");
        overlay.Loaded+=async(_,_)=>
        {
            string? failure=null;var success=false;var width=0;var height=0;var referenced=false;var pinned=false;var restored=false;
            try
            {
                await overlay.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                var frame=(CaptureFrame)Get("_frame")!;
                NativeMethods.GetWindowRect(new IntPtr(handle),out var rect);
                var bounds=ScreenCoordinateService.ToLocalDipRect(new ScreenRect(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top),frame.OriginX,frame.OriginY,overlay.ActualWidth,overlay.ActualHeight,frame.Image.PixelWidth,frame.Image.PixelHeight);
                var item=Invoke("CreateSelection",false)!;
                item.GetType().GetField("Bounds")!.SetValue(item,bounds);
                item.GetType().GetField("SnapshotTarget")!.SetValue(item,target);
                ((IList)Get("_selections")!).Add(item);Invoke("Select",0);
                ApplicationScrollState original;
                await using(var probe=await ApplicationScrollSession.StartAsync(target,CancellationToken.None))original=probe.Initial;
                await ((Task)Invoke("CaptureApplicationSnapshotAsync",item)!).WaitAsync(TimeSpan.FromMinutes(5));
                if(item.GetType().GetField("CapturedImageOverride")!.GetValue(item) is BitmapSource image)
                {
                    width=image.PixelWidth;height=image.PixelHeight;success=true;
                }
                failure=(string?)Get("_applicationSnapshotFailure");
                if(Get("_request") is not null)throw new InvalidOperationException("Unexpected AI request.");
                referenced=((IEnumerable)Get("_references")!).Cast<object>().Contains(item);
                pinned=app.Windows.OfType<PinnedImageWindow>().Any();
                await using(var probe=await ApplicationScrollSession.StartAsync(target,CancellationToken.None))restored=Math.Abs(probe.Initial.Position-original.Position)<=.2;
                success=success&&referenced&&pinned&&restored;
                if(failure is not null&&((FrameworkElement)overlay.FindName("ApplicationSnapshotFeedback")).Visibility!=Visibility.Visible)
                    throw new InvalidOperationException("Snapshot failure feedback is hidden.");
            }
            catch(Exception ex){failure=ex.GetType().Name;}
            finally
            {
                Directory.CreateDirectory(".codex-build/application-snapshot");
                File.WriteAllText(".codex-build/application-snapshot/live-replay.json",JsonSerializer.Serialize(new{success,width,height,referenced,pinned,restored,failure}));
                await Task.Delay(3000);
                foreach(var pin in app.Windows.OfType<PinnedImageWindow>().ToArray())pin.Close();
                overlay.Close();app.Shutdown(success?0:1);
            }
        };
        app.Run(overlay);
        object? Get(string name)=>typeof(CaptureOverlayWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay);
        object? Invoke(string name,params object?[] values)=>typeof(CaptureOverlayWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(overlay,values);
    }
}
