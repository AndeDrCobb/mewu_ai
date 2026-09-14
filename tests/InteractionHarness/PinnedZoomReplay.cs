// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using mewu_ai_Assistant.Interop;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Views;

internal static class PinnedZoomReplay
{
    internal static void Run(System.Windows.Application app)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var pixels=new byte[400*2400*4];for(var i=0;i<pixels.Length;i+=4){pixels[i]=(byte)(i/1600%255);pixels[i+1]=160;pixels[i+2]=80;pixels[i+3]=255;}
        var bitmap=BitmapSource.Create(400,2400,96,96,PixelFormats.Bgra32,null,pixels,1600);bitmap.Freeze();
        var pin=new PinnedImageWindow(bitmap,new ScreenRect(100,80,100,600),true);var checks=new Dictionary<string,bool>();
        pin.Loaded+=async(_,_)=>
        {
            string? failure=null;
            try
            {
                await pin.Dispatcher.InvokeAsync(()=>pin.UpdateLayout(),DispatcherPriority.ApplicationIdle);
                var view=(System.Windows.Controls.Image)typeof(PinnedImageWindow).GetField("_imageView",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(pin)!;
                var initialWidth=view.ActualWidth;
                for(var i=0;i<20;i++)
                {
                    pin.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.MouseWheelEvent});
                    await pin.Dispatcher.InvokeAsync(()=>pin.UpdateLayout(),DispatcherPriority.ApplicationIdle);
                }
                NativeMethods.GetWindowRect(new WindowInteropHelper(pin).Handle,out var rect);
                var image=(System.Windows.Controls.Image)typeof(PinnedImageWindow).GetField("_imageView",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(pin)!;
                checks["window-grows-beyond-screen-height"]=rect.Bottom-rect.Top>System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height;
                checks["actual-content-retains-six-to-one-ratio"]=Math.Abs(image.ActualHeight/image.ActualWidth-6)<.03;
                checks["image-actually-enlarged"]=image.ActualWidth>initialWidth*4;
                for(var i=0;i<20;i++){pin.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=Mouse.MouseWheelEvent});await pin.Dispatcher.InvokeAsync(()=>pin.UpdateLayout(),DispatcherPriority.ApplicationIdle);}
                checks["zoom-out-restores-original-scale"]=Math.Abs(image.ActualWidth-initialWidth)<5;
                if(checks.Values.Any(value=>!value))throw new InvalidOperationException($"Pinned zoom assertions failed: initial={initialWidth}, final={image.ActualWidth}, window={pin.ActualWidth}.");
            }
            catch(Exception ex){failure=ex.ToString();}
            finally{Directory.CreateDirectory(".codex-build/application-snapshot");File.WriteAllText(".codex-build/application-snapshot/zoom-replay.json",JsonSerializer.Serialize(new{checks,failure}));pin.Close();app.Shutdown(failure is null?0:1);}
        };
        pin.Show();app.Run();
    }
}
