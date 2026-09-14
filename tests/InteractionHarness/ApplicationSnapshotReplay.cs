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
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;

    internal static void RunBackground(bool web=false)
    {
        var app=new Application();
        var text=new TextBox{Text="SNAPSHOT_FIRST\n"+string.Join('\n',Enumerable.Range(1,200).Select(n=>$"Snapshot row {n:000}"))+"\nSNAPSHOT_LAST",IsReadOnly=true,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var window=new Window{Title="Mewu synthetic snapshot document",Width=600,Height=400,Left=120,Top=180,Content=text,Topmost=true};
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
                browser.NavigateToString("<!doctype html><html><head><title>Snapshot webpage</title></head><body><h1>SNAPSHOT_FIRST</h1>"+string.Concat(Enumerable.Range(1,200).Select(n=>$"<p style='margin:20px'>Snapshot row {n:000}</p>"))+"<table><tr><td>TABLE_LAST</td><td>200</td></tr></table><p>SNAPSHOT_LAST</p></body></html>");
                await navigated.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            window.UpdateLayout();Console.WriteLine(JsonSerializer.Serialize(ApplicationSnapshotTarget.FromWindow(new WindowInteropHelper(window).Handle)));
            while(await Task.Run(Console.ReadLine) is { } command)
            {
                if(command=="offset")Console.WriteLine(browser is null?text.VerticalOffset.ToString(System.Globalization.CultureInfo.InvariantCulture):await browser.ExecuteScriptAsync("window.scrollY"));
                else break;
            }
            browser?.Dispose();window.Close();
        };
        app.Run(window);
    }

    internal static void Run(Application app,AppHost host,bool web=false)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var overlay=new CaptureOverlayWindow(host);var checks=new Dictionary<string,bool>();
        var source=new Process{StartInfo=new(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        source.StartInfo.ArgumentList.Add(web?"--snapshot-web-background":"--snapshot-background");
        source.Start();var errors=source.StandardError.BaseStream.CopyToAsync(Stream.Null);
        overlay.Loaded+=async(_,_)=>
        {
            string? failure=null;
            try
            {
                var line=await source.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
                var target=JsonSerializer.Deserialize<ApplicationSnapshotTarget>(line!)!;
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
                var probe=await ApplicationSnapshotProcess.ReadAsync(target,CancellationToken.None);
                Check("direct-reader-has-last-row",probe.Text.Contains("SNAPSHOT_LAST"));
                await ((Task)Invoke("CaptureApplicationSnapshotAsync",item)!).WaitAsync(TimeSpan.FromSeconds(25));
                var text=(string?)item.GetType().GetField("SnapshotText")!.GetValue(item);
                Check("offscreen-last-row-retained",text?.Contains("SNAPSHOT_LAST")==true);
                if(web)Check("web-table-text-retained",text?.Contains("TABLE_LAST")==true);
                Check("snapshot-pinned",app.Windows.OfType<PinnedImageWindow>().Count()==1);
                if(item.GetType().GetField("CapturedImageOverride")!.GetValue(item) is System.Windows.Media.Imaging.BitmapSource image)
                {
                    var directory=Path.Combine(Environment.CurrentDirectory,".codex-build","application-snapshot");Directory.CreateDirectory(directory);
                    ScreenCaptureService.Save(image,Path.Combine(directory,web?"web-preview.png":"preview.png"),false);
                }
                var references=Get("_references");Check("snapshot-auto-referenced",(bool)references.GetType().GetMethod("Contains")!.Invoke(references,[item])!);
                Check("no-ai-request-started",Get("_request") is null);
                await source.StandardInput.WriteLineAsync("offset");await source.StandardInput.FlushAsync();
                Check("source-not-scrolled",await source.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))=="0");
                if(checks.Values.Any(value=>!value))throw new InvalidOperationException("Snapshot replay assertions failed.");
            }
            catch(Exception ex){failure=ex.ToString();}
            finally
            {
                var directory=Path.Combine(Environment.CurrentDirectory,".codex-build","application-snapshot");Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,web?"web-replay.json":"replay.json"),JsonSerializer.Serialize(new{checks,failure}));
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
}
