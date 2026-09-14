// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using mewu_ai_Assistant.Interop;
using mewu_ai_Assistant.Services;
using mewu_ai_Assistant.Views;
using Application=System.Windows.Application;
using TextBox=System.Windows.Controls.TextBox;
using Button=System.Windows.Controls.Button;
using Point=System.Windows.Point;

internal static class ApplicationSnapshotReplay
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]private struct PixelPoint{public int X,Y;}
    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern IntPtr WindowFromPoint(PixelPoint point);
    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern IntPtr GetAncestor(IntPtr handle,uint flag);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;

    internal static void RunBackground(bool web=false,bool graphicsOnly=false)
    {
        var app=new Application();
        var text=new TextBox{Text="SNAPSHOT_FIRST\n"+string.Join('\n',Enumerable.Range(1,200).Select(n=>$"Snapshot row {n:000}"))+"\nSNAPSHOT_LAST",IsReadOnly=true,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var window=new Window{Title="Mewu synthetic snapshot document",Width=600,Height=400,Left=120,Top=180,Content=text,Topmost=true};
        window.SourceInitialized+=(_,_)=>{var disabled=1;System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(new WindowInteropHelper(window).Handle,3,ref disabled,4));};
        if(graphicsOnly)window.Content=new Border{Background=System.Windows.Media.Brushes.RoyalBlue,Child=new System.Windows.Shapes.Ellipse{Width=140,Height=140,Fill=System.Windows.Media.Brushes.Gold}};
        Microsoft.Web.WebView2.Wpf.WebView2? browser=null;
        if(web){browser=new();window.Content=browser;}
        window.Loaded+=async(_,_)=>
        {
            if(browser is not null)
            {
                var environment=await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null,Path.Combine(Environment.CurrentDirectory,".codex-build","application-snapshot","webview-profile"));
                await browser.EnsureCoreWebView2Async(environment);
                var navigated=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                browser.NavigationCompleted+=(_,_)=>navigated.TrySetResult();
                browser.NavigateToString("<!doctype html><html><head><title>Snapshot webpage</title></head><body><h1>SNAPSHOT_FIRST</h1><svg width='180' height='90'><circle cx='60' cy='45' r='40' fill='#ff0000'/></svg>"+string.Concat(Enumerable.Range(1,80).Select(n=>$"<p style='margin:20px'>Snapshot row {n:000}</p>"))+"<table style='background:#00ff00'><tr><td>TABLE_LAST</td><td>200</td></tr></table><p>SNAPSHOT_LAST</p></body></html>");
                await navigated.Task.WaitAsync(TimeSpan.FromSeconds(15));
                await browser.ExecuteScriptAsync("window.scrollTo(0,530)");
            }
            if(browser is null&&!graphicsOnly)text.ScrollToVerticalOffset(120);
            window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);NativeMethods.FlushComposition();
            Console.WriteLine(JsonSerializer.Serialize(ApplicationSnapshotTarget.FromWindow(new WindowInteropHelper(window).Handle)));
            while(await Task.Run(Console.ReadLine) is { } command)
            {
                if(command=="offset")Console.WriteLine(browser is null?text.VerticalOffset.ToString(System.Globalization.CultureInfo.InvariantCulture):await browser.ExecuteScriptAsync("window.scrollY"));
                else break;
            }
            browser?.Dispose();window.Close();
        };
        app.Run(window);
    }

    internal static void Run(Application app,AppHost host,bool web=false,bool graphicsOnly=false,bool cancel=false,bool close=false)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var overlay=new CaptureOverlayWindow(host);Program.MarkReplayWindow(overlay,"应用快照验收 · 合成文档 · 完成后自动关闭");var checks=new Dictionary<string,bool>();
        // The replay invokes the real selection handlers explicitly. Ignore
        // unrelated physical pointer input while its fixture is being prepared.
        ((FrameworkElement)overlay.FindName("Root")).IsHitTestVisible=false;
        var source=new Process{StartInfo=new(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        source.StartInfo.ArgumentList.Add(graphicsOnly?"--snapshot-graphics-background":web?"--snapshot-web-background":"--snapshot-background");
        source.Start();var errors=source.StandardError.BaseStream.CopyToAsync(Stream.Null);
        overlay.Loaded+=async(_,_)=>
        {
            string? failure=null;
            try
            {
                var line=await source.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
                var target=JsonSerializer.Deserialize<ApplicationSnapshotTarget>(line!)!;
                var overlayHandle=new WindowInteropHelper(overlay).Handle;
                if(!NativeMethods.ExcludeFromCapture(overlayHandle,true))throw new InvalidOperationException("Cannot capture synthetic fixture safely.");
                var sourceFrame=new ScreenCaptureService().CaptureDesktop();Set("_frame",sourceFrame);((System.Windows.Controls.Image)overlay.FindName("DesktopImage")).Source=sourceFrame.Image;
                NativeMethods.ApplyPresentationCaptureVisibility(overlayHandle,host.Settings.TeachingMode);
                Set("_conversationAiAvailable",true);
                var frame=(CaptureFrame)Get("_frame");
                NativeMethods.GetWindowRect(new IntPtr(target.Handle),out var rectangle);
                var bounds=ScreenCoordinateService.ToLocalDipRect(new mewu_ai_Assistant.Models.ScreenRect(rectangle.Left,rectangle.Top,rectangle.Right-rectangle.Left,rectangle.Bottom-rectangle.Top),frame.OriginX,frame.OriginY,overlay.ActualWidth,overlay.ActualHeight,frame.Image.PixelWidth,frame.Image.PixelHeight);
                var point=new Point(bounds.Left+bounds.Width/2,bounds.Top+60);
                var resolved=(ApplicationSnapshotTarget?)Invoke("ResolveSnapshotTarget",point);
                Check("snap-keeps-original-window",resolved==target);
                var item=Invoke("CreateSelection",false)!;item.GetType().GetField("Bounds")!.SetValue(item,bounds);
                ((IList)Get("_selections")).Add(item);Invoke("Select",0);
                Invoke("UpdateApplicationSnapshotTool",item);
                Check("manual-selection-retains-scroll-tool",((Button)overlay.FindName("LongCaptureButton")).ToolTip.ToString()!.Contains("滚动"));
                Set("_pendingAutoSelection",bounds);Set("_pendingSnapshotTarget",resolved);Set("_selecting",true);
                Invoke("OnMouseUp",overlay,new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Mouse.MouseUpEvent});
                Check("auto-selection-binds-window",Equals(item.GetType().GetField("SnapshotTarget")!.GetValue(item),target));
                Invoke("UpdateApplicationSnapshotTool",item);
                Check("auto-selection-switches-snapshot-tool",((Button)overlay.FindName("LongCaptureButton")).ToolTip.ToString()!.Contains("应用快照"));
                await source.StandardInput.WriteLineAsync("offset");await source.StandardInput.FlushAsync();
                var initialOffset=await source.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
                NativeMethods.SetWindowPos(overlayHandle,new IntPtr(-1),0,0,0,0,0x0001|0x0002);
                await overlay.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);await Task.Delay(200);
                byte[] ForegroundPixels()
                {
                    var desktop=new ScreenCaptureService().CaptureDesktop();
                    return Pixels(new System.Windows.Media.Imaging.CroppedBitmap(desktop.Image,new Int32Rect(rectangle.Left-desktop.OriginX+100,rectangle.Top-desktop.OriginY+170,128,96)));
                }
                var frozenPixels=ForegroundPixels();
                ScreenCaptureService.Save(System.Windows.Media.Imaging.BitmapSource.Create(128,96,96,96,System.Windows.Media.PixelFormats.Bgra32,null,frozenPixels,128*4),Path.GetFullPath(".codex-build/application-snapshot/foreground-before.png"),false);var frozen=((System.Windows.Controls.Image)overlay.FindName("DesktopImage")).Source;
                var captureTask=(Task)Invoke("CaptureApplicationSnapshotAsync",item)!;
                var observedMovement=false;var frozenThroughout=true;var sameSource=true;var noHole=true;var timer=Stopwatch.StartNew();
                while(!captureTask.IsCompleted&&timer.Elapsed<TimeSpan.FromSeconds(120))
                {
                    await source.StandardInput.WriteLineAsync("offset");await source.StandardInput.FlushAsync();
                    var offset=await source.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    if(!captureTask.IsCompleted&&offset!=initialOffset)
                    {
                        observedMovement=true;
                        var desktopImage=(System.Windows.Controls.Image)overlay.FindName("DesktopImage");
                        sameSource&=ReferenceEquals(frozen,desktopImage.Source);noHole&=desktopImage.Clip is null;var current=ForegroundPixels();
                        if(host.Settings.TeachingMode&&frozenThroughout&&!SameForeground(frozenPixels,current))
                        {
                            ScreenCaptureService.Save(System.Windows.Media.Imaging.BitmapSource.Create(128,96,96,96,System.Windows.Media.PixelFormats.Bgra32,null,current,128*4),Path.GetFullPath(".codex-build/application-snapshot/foreground-after.png"),false);
                            Check("overlay-on-top-at-first-change",GetAncestor(WindowFromPoint(new PixelPoint{X=rectangle.Left+150,Y=rectangle.Top+200}),2)==overlayHandle);Check("overlay-shared-at-first-change",NativeMethods.IsVisibleToCapture(overlayHandle));
                        }
                        frozenThroughout&=host.Settings.TeachingMode?SameForeground(frozenPixels,current):GetAncestor(WindowFromPoint(new PixelPoint{X=rectangle.Left+150,Y=rectangle.Top+200}),2)==overlayHandle;
                        if(cancel)Invoke("HandleEscape");if(close)overlay.Close();
                    }
                    await Task.Delay(80);
                }
                await captureTask.WaitAsync(TimeSpan.FromSeconds(15));
                Check(host.Settings.TeachingMode?"foreground-pixels-remain-fixed":"protected-foreground-retains-hit-testing",frozenThroughout);Check("same-frozen-source",sameSource);Check("no-live-hole",noHole);
                Check("offscreen-rendering-observed",graphicsOnly||observedMovement);
                Check("snapshot-pinned",app.Windows.OfType<PinnedImageWindow>().Count()==(cancel||close?0:1));
                Check("no-text-image-substitution",item.GetType().GetField("SnapshotText")!.GetValue(item) is null);
                if(item.GetType().GetField("CapturedImageOverride")!.GetValue(item) is System.Windows.Media.Imaging.BitmapSource image)
                {
                    var directory=Path.Combine(Environment.CurrentDirectory,".codex-build","application-snapshot");Directory.CreateDirectory(directory);
                    ScreenCaptureService.Save(image,Path.Combine(directory,graphicsOnly?"graphics-preview.png":web?"web-preview.png":"preview.png"),false);
                    Check("captures-offscreen-pixels",graphicsOnly||image.PixelHeight>2000);
                    if(web)
                    {
                        var pixels=Pixels(image);var red=false;var green=false;
                        for(var i=0;i<pixels.Length;i+=4){if(pixels[i+2]>240&&pixels[i+1]<20&&pixels[i]<20)red=true;if(pixels[i+1]>240&&pixels[i+2]<20&&pixels[i]<20)green=true;}
                        Check("original-svg-retained",red);Check("offscreen-table-color-retained",green);
                    }
                }
                var references=Get("_references");Check("snapshot-auto-referenced",(bool)references.GetType().GetMethod("Contains")!.Invoke(references,[item])! == !(cancel||close));
                Check("no-ai-request-started",Get("_request") is null);
                await source.StandardInput.WriteLineAsync("offset");await source.StandardInput.FlushAsync();
                Check("source-position-restored",await source.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))==initialOffset);
                if(close)Check("close-waits-for-restoration",!overlay.IsVisible);
                if(cancel||close)Check("cancellation-preserves-original-image",item.GetType().GetField("CapturedImageOverride")!.GetValue(item) is null);
                if(!checks["snapshot-pinned"])throw new InvalidOperationException(((System.Windows.Controls.TextBlock)overlay.FindName("PromptStatus")).Text+" "+Get("_applicationSnapshotFailure"));
                if(checks.Values.Any(value=>!value))throw new InvalidOperationException("Snapshot replay assertions failed.");
            }
            catch(Exception ex){failure=ex.ToString();}
            finally
            {
                var directory=Path.Combine(Environment.CurrentDirectory,".codex-build","application-snapshot");Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,close?"close-replay.json":cancel?"cancel-replay.json":graphicsOnly?"graphics-replay.json":web?"web-replay.json":"replay.json"),JsonSerializer.Serialize(new{checks,failure}));
                foreach(var pin in app.Windows.OfType<PinnedImageWindow>().ToArray())pin.Close();
                if(!source.HasExited){source.StandardInput.Close();if(!source.WaitForExit(3000))source.Kill(true);}
                await errors;source.Dispose();overlay.Close();app.Shutdown(failure is null?0:1);
            }
        };
        app.Run(overlay);
        object Get(string name)=>typeof(CaptureOverlayWindow).GetField(name,Private)!.GetValue(overlay)!;
        void Set(string name,object? value)=>typeof(CaptureOverlayWindow).GetField(name,Private)!.SetValue(overlay,value);
        object? Invoke(string name,params object?[] values)=>typeof(CaptureOverlayWindow).GetMethod(name,Private)!.Invoke(overlay,values);
        void Check(string name,bool value)=>checks.Add(name,value);
    }

    private static bool SameForeground(byte[] first,byte[] second)
    {
        // Compare actual foreground pixels, not just the frozen WPF source.
        return first.AsSpan().SequenceEqual(second);
    }

    private static byte[] Pixels(System.Windows.Media.Imaging.BitmapSource source)
    {
        var formatted=new System.Windows.Media.Imaging.FormatConvertedBitmap(source,System.Windows.Media.PixelFormats.Bgra32,null,0);
        var bytes=new byte[source.PixelWidth*source.PixelHeight*4];formatted.CopyPixels(bytes,source.PixelWidth*4,0);return bytes;
    }
}
