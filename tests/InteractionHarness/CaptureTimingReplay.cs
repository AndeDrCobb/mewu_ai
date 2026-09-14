// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using mewu_ai_Assistant.Interop;
using mewu_ai_Assistant.Services;
using mewu_ai_Assistant.Views;
using Application=System.Windows.Application;
using Brushes=System.Windows.Media.Brushes;

internal static class CaptureTimingReplay
{
    internal static void Run(Application app,AppHost host)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;host.Settings.CaptureDelaySeconds=0;
        var marker=new Border{Background=Brushes.Lime};
        var fixture=new Window{Title="即时截图验收 · 合成色块",Content=marker,Left=120,Top=180,Width=600,Height=400,Topmost=true};
        var checks=new Dictionary<string,bool>();
        fixture.SourceInitialized+=(_,_)=>{var disabled=1;DwmSetWindowAttribute(new WindowInteropHelper(fixture).Handle,3,ref disabled,4);};
        fixture.Loaded+=async(_,_)=>
        {
            string? failure=null;CaptureOverlayWindow? overlay=null;
            try
            {
                await fixture.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);NativeMethods.FlushComposition();
                var center=marker.PointToScreen(new System.Windows.Point(marker.ActualWidth/2,marker.ActualHeight/2));
                host.BeginCapture();
                overlay=app.Windows.OfType<CaptureOverlayWindow>().SingleOrDefault();
                checks["zero-delay-capture-starts-without-another-dispatch-turn"]=overlay is not null;
                if(overlay is null)throw new InvalidOperationException("Capture was deferred.");
                Program.MarkReplayWindow(overlay,"即时截图验收 · 完成后自动关闭");
                var frame=(CaptureFrame)typeof(CaptureOverlayWindow).GetField("_frame",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                marker.Background=Brushes.Red;
                await fixture.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);NativeMethods.FlushComposition();
                var pixel=new byte[4];frame.Image.CopyPixels(new Int32Rect((int)center.X-frame.OriginX,(int)center.Y-frame.OriginY,1,1),pixel,4,0);
                checks["frozen-image-retains-trigger-time-color"]=pixel[1]>240&&pixel[0]<10&&pixel[2]<10;
                if(checks.Values.Any(value=>!value))throw new InvalidOperationException("Immediate capture assertions failed.");
            }
            catch(Exception ex){failure=ex.ToString();}
            finally
            {
                Directory.CreateDirectory(".codex-build/application-snapshot");
                File.WriteAllText(".codex-build/application-snapshot/timing-replay.json",JsonSerializer.Serialize(new{checks,failure}));
                overlay?.Close();fixture.Close();app.Shutdown(failure is null?0:1);
            }
        };
        app.Run(fixture);
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}
