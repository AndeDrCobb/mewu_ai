// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Collections;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using mewu_ai_Assistant.Views;
using Application=System.Windows.Application;
using Point=System.Windows.Point;
using Brushes=System.Windows.Media.Brushes;
using ContextMenu=System.Windows.Controls.ContextMenu;
using MenuItem=System.Windows.Controls.MenuItem;

internal static class PointerPassThroughReplay
{
    private const string DirectoryPath=".codex-build/pointer-pass-through";
    private static string StatePath=>Path.Combine(DirectoryPath,"target.json");
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed record State(long Handle,int X,int Y,int LeftDown,int LeftUp,int RightDown,int RightUp,int Moves,int LastX,int LastY,bool MenuOpen=false,int MenuX=0,int MenuY=0,bool MenuSelected=false);

    internal static void RunTarget()
    {
        var app=new Application();var surface=new Border{Background=Brushes.CornflowerBlue};
        var window=new Window{Title="Pointer replay target",Topmost=true,Left=150,Top=120,Width=650,Height=430,Content=surface};
        var menu=new ContextMenu();var action=new MenuItem{Header="Native context menu probe"};menu.Items.Add(action);surface.ContextMenu=menu;
        var state=new State(0,0,0,0,0,0,0,0,0,0);
        void Save()
        {
            var temp=StatePath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(state));File.Move(temp,StatePath,true);
        }
        window.SourceInitialized+=(_,_)=>{var hwnd=new WindowInteropHelper(window).Handle;var disabled=1;DwmSetWindowAttribute(hwnd,3,ref disabled,4);};
        menu.Opened+=(_,_)=>{var point=action.PointToScreen(new Point(20,12));state=state with{MenuOpen=true,MenuX=(int)point.X,MenuY=(int)point.Y};Save();};
        menu.Closed+=(_,_)=>{state=state with{MenuOpen=false};Save();};
        action.Click+=(_,_)=>{state=state with{MenuSelected=true};Save();};
        window.Loaded+=async(_,_)=>
        {
            await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            window.Show();window.Activate();window.Topmost=false;
            var point=surface.PointToScreen(new Point(120,100));state=state with{Handle=new WindowInteropHelper(window).Handle.ToInt64(),X=(int)point.X,Y=(int)point.Y};Save();
        };
        surface.MouseLeftButtonDown+=(_,e)=>{state=state with{LeftDown=state.LeftDown+1};surface.Background=state.LeftDown%2==0?Brushes.CornflowerBlue:Brushes.Orange;surface.CaptureMouse();Save();e.Handled=true;};
        surface.MouseLeftButtonUp+=(_,e)=>{state=state with{LeftUp=state.LeftUp+1};surface.ReleaseMouseCapture();Save();e.Handled=true;};
        surface.MouseRightButtonDown+=(_,_)=>{state=state with{RightDown=state.RightDown+1};Save();};
        surface.MouseRightButtonUp+=(_,_)=>{state=state with{RightUp=state.RightUp+1};Save();};
        surface.MouseMove+=(_,e)=>
        {
            if(e.LeftButton!=MouseButtonState.Pressed)return;
            var point=surface.PointToScreen(e.GetPosition(surface));state=state with{Moves=state.Moves+1,LastX=(int)point.X,LastY=(int)point.Y};Save();
        };
        app.Run(window);
    }

    internal static void Run(Application app,AppHost host)
    {
        Directory.CreateDirectory(DirectoryPath);File.Delete(StatePath);app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        using var target=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--pass-through-target"){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden})!;
        var checks=new List<string>();CaptureOverlayWindow? overlay=null;string? failure=null;
        app.Dispatcher.BeginInvoke(new Action(async()=>
        {
            try
            {
                var state=await Wait(s=>s.Handle!=0);overlay=NewOverlay();
                await Ready(overlay);
                var reference=typeof(CaptureOverlayWindow).GetMethod("CreateSelection",Private)!.Invoke(overlay,[false])!;
                var root=(FrameworkElement)overlay.FindName("Root");var local=root.PointFromScreen(new Point(state.X,state.Y));
                var bounds=new Rect(local.X,local.Y,30,30);reference.GetType().GetField("Bounds")!.SetValue(reference,bounds);
                ((IList)typeof(CaptureOverlayWindow).GetField("_selections",Private)!.GetValue(overlay)!).Add(reference);
                typeof(CaptureOverlayWindow).GetMethod("UpdateSelection",Private)!.Invoke(overlay,[reference]);
                var frame=(CaptureFrame)typeof(CaptureOverlayWindow).GetField("_frame",Private)!.GetValue(overlay)!;
                var pixels=(Int32Rect)typeof(CaptureOverlayWindow).GetMethod("ToPixelRect",Private)!.Invoke(overlay,[bounds])!;
                var original=PixelBytes(ScreenCaptureService.Crop(frame.Image,pixels));
                MouseInput(state.X,state.Y,0x0008);MouseInput(state.X,state.Y,0x0010);
                await Wait(s=>s.LeftDown==1&&s.LeftUp==1);await Restored(overlay);
                Require(Read().RightDown==0&&Read().RightUp==0,"Plain right click leaked secondary-button events");checks.Add("right-click-delivers-one-left-click");
                var retained=(BitmapSource)reference.GetType().GetField("CapturedImageOverride")!.GetValue(reference)!;
                Require(original.SequenceEqual(PixelBytes(retained)),"Desktop refresh changed existing reference pixels");
                checks.Add("existing-reference-pixels-survive-desktop-refresh");

                state=Read();overlay.Activate();await Ready(overlay);
                MouseInput(state.X,state.Y,0x0008);await Wait(s=>s.LeftDown==2);
                KeyboardInput(0x11,false); // Changing Ctrl during a left drag must not change its up.
                MouseInput(state.X+110,state.Y+80,0);MouseInput(state.X+150,state.Y+95,0);
                MouseInput(state.X+150,state.Y+95,0x0010);KeyboardInput(0x11,true);
                state=await Wait(s=>s.LeftUp==2);await Restored(overlay);
                Require(state.Moves>0&&Math.Abs(state.LastX-state.X-150)<=2,"Forwarded drag did not move under a held primary button");
                Require(state.RightDown==0&&state.RightUp==0,"Changing Ctrl split the button pair");checks.Add("held-right-drags-and-modifier-change-keeps-paired-up");

                // The native receiver is a separate process. Block WPF until
                // that receiver confirms up, proving release uses its own pump.
                overlay.Activate();await Ready(overlay);MouseInput(state.X,state.Y,0x0008);await Wait(s=>s.LeftDown==3);
                using(var blocked=new ManualResetEventSlim())using(var released=new ManualResetEventSlim())
                {
                    var background=Task.Run(async()=>
                    {
                        if(!blocked.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException();
                        try{MouseInput(state.X,state.Y,0x0010);await Wait(s=>s.LeftUp==3);}
                        finally{released.Set();}
                    });
                    blocked.Set();Require(released.Wait(TimeSpan.FromSeconds(10)),"UI stall prevented paired release");await background;
                }
                await Restored(overlay);checks.Add("release-works-while-overlay-ui-is-blocked");

                overlay.Activate();await Ready(overlay);MouseInput(state.X,state.Y,0x0008);await Wait(s=>s.LeftDown==4);
                overlay.Close();await Wait(s=>s.LeftUp==4);MouseInput(state.X,state.Y,0x0010);
                await Task.Delay(100);Require(Read().RightUp==0,"Closing leaked a stray right up");checks.Add("close-during-drag-releases-left-without-stray-context-menu");

                overlay=NewOverlay();await Ready(overlay);state=Read();
                KeyboardInput(0x11,false);MouseInput(state.X,state.Y,0x0008);await Wait(s=>s.RightDown==1);
                KeyboardInput(0x11,true);MouseInput(state.X,state.Y,0x0010);
                await Wait(s=>s.RightUp==1);await Restored(overlay);
                Require(Read().LeftDown==4&&Read().LeftUp==4,"Ctrl-right also emitted a primary click");checks.Add("ctrl-right-delivers-a-paired-native-right-click");
                state=await Wait(s=>s.MenuOpen);await Ready(overlay);
                MouseInput(state.MenuX,state.MenuY,0x0002);MouseInput(state.MenuX,state.MenuY,0x0004);
                await Wait(s=>s.MenuSelected);checks.Add("underlying-context-menu-remains-clickable");
                overlay.Activate();await Ready(overlay);state=Read();
                MouseInput(state.X,state.Y,0x0008);MouseInput(state.X,state.Y,0x0010);
                // Start the next down before the 60 ms overlay restoration.
                await Wait(s=>s.LeftUp==5);
                MouseInput(state.X,state.Y,0x0008);MouseInput(state.X,state.Y,0x0010);
                await Wait(s=>s.LeftDown==6&&s.LeftUp==6);await Restored(overlay);
                checks.Add("rapid-repeated-clicks-retain-both-button-pairs");

                overlay.Activate();await Ready(overlay);
                var service=(RightButtonPassThrough)typeof(CaptureOverlayWindow).GetField("_rightPassThrough",Private)!.GetValue(overlay)!;
                using(var blocked=new ManualResetEventSlim())using(var cancelled=new ManualResetEventSlim())
                {
                    var background=Task.Run(async()=>
                    {
                        if(!blocked.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException();
                        try
                        {
                            MouseInput(state.X,state.Y,0x0008);
                            var deadline=Stopwatch.StartNew();while(!service.IsActive&&deadline.Elapsed<TimeSpan.FromSeconds(10))await Task.Delay(10);
                            Require(service.IsActive,"Pending gesture was not intercepted");service.Cancel();MouseInput(state.X,state.Y,0x0010);
                        }
                        finally{cancelled.Set();}
                    });
                    blocked.Set();Require(cancelled.Wait(TimeSpan.FromSeconds(10)),"Pending cancellation blocked");await background;
                }
                await Restored(overlay);await overlay.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                Require(Read().LeftDown==6&&Read().LeftUp==6,"Cancelled preparation replayed a delayed click");
                checks.Add("cancel-before-visual-ready-does-not-replay-late-input");
                typeof(CaptureOverlayWindow).GetField("_conversationAiAvailable",Private)!.SetValue(overlay,true);
                ((FrameworkElement)overlay.FindName("PromptBarHost")).Visibility=Visibility.Visible;
                ((FrameworkElement)overlay.FindName("PromptBarHost")).IsHitTestVisible=true;
                typeof(CaptureOverlayWindow).GetField("_promptBarHidden",Private)!.SetValue(overlay,true);
                typeof(CaptureOverlayWindow).GetMethod("SetPromptBarHidden",Private)!.Invoke(overlay,[false,false]);
                typeof(CaptureOverlayWindow).GetMethod("PositionPromptBar",Private)!.Invoke(overlay,null);
                await overlay.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                var revealDeadline=Stopwatch.StartNew();while((bool)typeof(CaptureOverlayWindow).GetField("_promptBarVisibilityAnimating",Private)!.GetValue(overlay)!&&revealDeadline.Elapsed<TimeSpan.FromSeconds(10))await Task.Delay(20);
                var prompt=(System.Windows.Controls.TextBox)overlay.FindName("QuickPrompt");
                var promptPoint=prompt.PointToScreen(new Point(prompt.ActualWidth/2,prompt.ActualHeight/2));
                await Ready(overlay);
                var policy=(PointerPassThroughPolicy)typeof(CaptureOverlayWindow).GetMethod("BuildRightPassThroughPolicy",Private)!.Invoke(overlay,null)!;
                Require(prompt.IsVisible&&!policy.Allows((int)promptPoint.X,(int)promptPoint.Y,Environment.TickCount64),"Composer right-click area was not excluded");
                var contextOpened=false;prompt.AddHandler(ContextMenuService.ContextMenuOpeningEvent,new ContextMenuEventHandler((_,_)=>contextOpened=true),true);
                MouseInput((int)promptPoint.X,(int)promptPoint.Y,0x0008);MouseInput((int)promptPoint.X,(int)promptPoint.Y,0x0010);
                var menuDeadline=Stopwatch.StartNew();while(!contextOpened&&menuDeadline.Elapsed<TimeSpan.FromSeconds(10))await Task.Delay(20);
                Require(contextOpened&&!service.IsActive,"Composer right click did not retain its own context menu");
                checks.Add("composer-retains-native-right-click-menu");
                overlay.Close();
            }
            catch(Exception ex){failure=ex.ToString();}
            finally
            {
                KeyboardInput(0x11,true);overlay?.Close();
                // Release test-origin buttons too, even when an assertion fails.
                var point=Read();MouseInput(point.X,point.Y,0x0010|0x0004);
                target.CloseMainWindow();if(!await Task.Run(()=>target.WaitForExit(3000)))target.Kill();
                File.WriteAllText(Path.Combine(DirectoryPath,"replay.json"),JsonSerializer.Serialize(new{checks,failure}));app.Shutdown(failure is null?0:1);
            }
        }));
        app.Run();
        CaptureOverlayWindow NewOverlay(){var result=new CaptureOverlayWindow(host);Program.MarkReplayWindow(result,"右键穿透检查 · 独立测试窗口");result.Show();result.Activate();return result;}
    }

    private static async Task Ready(CaptureOverlayWindow window)
    {
        var watch=Stopwatch.StartNew();
        while(watch.Elapsed<TimeSpan.FromSeconds(10))
        {
            typeof(CaptureOverlayWindow).GetMethod("UpdateRightPassThroughPolicy",Private)!.Invoke(window,null);
            if(typeof(CaptureOverlayWindow).GetField("_rightPassThroughPolicyTimer",Private)!.GetValue(window) is not null)return;
            await Task.Delay(20);
        }
        throw new TimeoutException("Input routing did not start");
    }
    private static async Task Restored(CaptureOverlayWindow window)
    {
        var watch=Stopwatch.StartNew();
        while(watch.Elapsed<TimeSpan.FromSeconds(10))
        {
            var service=(RightButtonPassThrough?)typeof(CaptureOverlayWindow).GetField("_rightPassThrough",Private)!.GetValue(window);
            if(service is {IsActive:false}&&((FrameworkElement)window.FindName("Root")).Opacity==1)return;
            await Task.Delay(20);
        }
        throw new TimeoutException("Overlay input/visuals were not restored");
    }
    private static State Read()
    {
        try{return JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath))!;}catch(IOException){return new(0,0,0,0,0,0,0,0,0,0);}
    }
    private static async Task<State> Wait(Func<State,bool> condition)
    {
        var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(10)){var state=Read();if(condition(state))return state;await Task.Delay(25);}
        throw new TimeoutException("Native pointer receiver did not observe the expected event: "+JsonSerializer.Serialize(Read()));
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static byte[] PixelBytes(BitmapSource image){var stride=(image.PixelWidth*image.Format.BitsPerPixel+7)/8;var data=new byte[stride*image.PixelHeight];image.CopyPixels(data,stride,0);return data;}
    private static void MouseInput(int x,int y,uint flags)
    {
        var area=System.Windows.Forms.SystemInformation.VirtualScreen;var point=ScreenCoordinateService.ToAbsoluteMousePoint(x,y,new(area.X,area.Y,area.Width,area.Height));
        Send([new Input{Mouse=new(){Dx=point.X,Dy=point.Y,Flags=0x8000|0x4000|0x0001|flags,Extra=new UIntPtr(0x51415054)}}]);
    }
    private static void KeyboardInput(ushort key,bool up)=>Send([new Input{Type=1,Keyboard=new(){Key=key,Flags=up?2u:0}}]);
    private static void Send(Input[] inputs){if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<Input>())!=inputs.Length)throw new InvalidOperationException("Test input injection failed");}
    [StructLayout(LayoutKind.Explicit,Size=40)]private struct Input{[FieldOffset(0)]public uint Type;[FieldOffset(8)]public MouseData Mouse;[FieldOffset(8)]public KeyboardData Keyboard;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseData{public int Dx,Dy;public uint Data,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct KeyboardData{public ushort Key,Scan;public uint Flags,Time;public UIntPtr Extra;}
    [DllImport("user32.dll")]private static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}
