// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Runtime.InteropServices;
using System.Windows.Threading;
using mewu_ai_Assistant.Interop;
using mewu_ai_Assistant.Models;

namespace mewu_ai_Assistant.Services;

/// <summary>
/// Forwards mouse input that lands outside the recording controls while the
/// full-screen teaching overlay remains visible.  Returning HTTRANSPARENT is
/// only reliable within one UI thread, so recording uses this low-level hook
/// for clicks in other desktop processes as well.
/// </summary>
internal sealed class RecordingMousePassThrough : IDisposable
{
    private const uint InputMarker=0x4D455752;
    private const uint Move=0x0001,LeftDown=0x0002,LeftUp=0x0004,RightDown=0x0008,RightUp=0x0010,
        MiddleDown=0x0020,MiddleUp=0x0040,Wheel=0x0800,Absolute=0x8000,VirtualDesk=0x4000;
    private const int WmMouseMove=0x0200,WmLButtonDown=0x0201,WmLButtonUp=0x0202,WmRButtonDown=0x0204,WmRButtonUp=0x0205,
        WmMButtonDown=0x0207,WmMButtonUp=0x0208,WmMouseWheel=0x020A;
    private readonly object _gate=new();
    private readonly IntPtr _window;
    private readonly HookProcedure _procedure;
    private readonly TaskCompletionSource<bool> _started=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PointerPassThroughPolicy _policy=new(false,[],0);
    private Dispatcher? _dispatcher;
    private IntPtr _hook;
    private bool _disposed;
    private uint _activeUp;

    internal RecordingMousePassThrough(IntPtr window)
    {
        _window=window;
        _procedure=Hook;
    }

    internal void Update(PointerPassThroughPolicy policy)=>Volatile.Write(ref _policy,policy);

    internal bool Start()
    {
        if(_window==IntPtr.Zero)return false;
        var thread=new Thread(Run){IsBackground=true,Name="Mewu recording input routing"};
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return _started.Task.Wait(TimeSpan.FromSeconds(2))&&_started.Task.Result;
    }

    private void Run()
    {
        _dispatcher=Dispatcher.CurrentDispatcher;
        try
        {
            _hook=SetWindowsHookEx(14,_procedure,GetModuleHandle(null),0);
            _started.TrySetResult(_hook!=IntPtr.Zero);
            if(_hook!=IntPtr.Zero)Dispatcher.Run();
        }
        catch{_started.TrySetResult(false);}
        finally
        {
            lock(_gate)
            {
                if(_activeUp!=0)Inject(CurrentCursor(),_activeUp);
                _activeUp=0;
                NativeMethods.TrySetWindowMouseTransparent(_window,false);
            }
            if(_hook!=IntPtr.Zero)UnhookWindowsHookEx(_hook);
            _hook=IntPtr.Zero;
        }
    }

    private IntPtr Hook(int code,IntPtr message,IntPtr data)
    {
        if(code<0)return CallNextHookEx(_hook,code,message,data);
        var input=Marshal.PtrToStructure<MouseHookData>(data);
        if(input.ExtraInfo.ToUInt64()==InputMarker)return CallNextHookEx(_hook,code,message,data);
        var kind=unchecked((uint)message.ToInt64());
        lock(_gate)
        {
            if(_disposed)return CallNextHookEx(_hook,code,message,data);
            var allowed=Volatile.Read(ref _policy).Allows(input.Point.X,input.Point.Y,Environment.TickCount64);
            if(kind==WmMouseMove)
            {
                if(_activeUp!=0||allowed)
                {
                    NativeMethods.TrySetWindowMouseTransparent(_window,true);
                    if(Inject(input.Point,0))return new IntPtr(1);
                }
                if(_activeUp==0)NativeMethods.TrySetWindowMouseTransparent(_window,false);
                return CallNextHookEx(_hook,code,message,data);
            }
            if(kind==WmLButtonDown||kind==WmRButtonDown||kind==WmMButtonDown)
            {
                if(!allowed||_activeUp!=0)
                {
                    NativeMethods.TrySetWindowMouseTransparent(_window,false);
                    return CallNextHookEx(_hook,code,message,data);
                }
                _activeUp=kind switch{WmLButtonDown=>LeftUp,WmRButtonDown=>RightUp,_=>MiddleUp};
                NativeMethods.TrySetWindowMouseTransparent(_window,true);
                var down=kind switch{WmLButtonDown=>LeftDown,WmRButtonDown=>RightDown,_=>MiddleDown};
                if(Inject(input.Point,down))return new IntPtr(1);
                _activeUp=0;NativeMethods.TrySetWindowMouseTransparent(_window,false);
                return CallNextHookEx(_hook,code,message,data);
            }
            if(kind==WmLButtonUp||kind==WmRButtonUp||kind==WmMButtonUp)
            {
                if(_activeUp==0)return CallNextHookEx(_hook,code,message,data);
                var up=_activeUp;_activeUp=0;
                var injected=Inject(input.Point,up);
                NativeMethods.TrySetWindowMouseTransparent(_window,false);
                return injected?new IntPtr(1):CallNextHookEx(_hook,code,message,data);
            }
            if(kind==WmMouseWheel&&allowed)
            {
                NativeMethods.TrySetWindowMouseTransparent(_window,true);
                return InjectWheel(input.Point,input.MouseData)?new IntPtr(1):CallNextHookEx(_hook,code,message,data);
            }
            if(_activeUp==0)NativeMethods.TrySetWindowMouseTransparent(_window,false);
            return CallNextHookEx(_hook,code,message,data);
        }
    }

    private static NativePoint CurrentCursor()
    {
        GetCursorPos(out var point);
        return point;
    }

    private static bool Inject(NativePoint point,uint button)
    {
        var desktop=new ScreenRect(GetSystemMetrics(76),GetSystemMetrics(77),GetSystemMetrics(78),GetSystemMetrics(79));
        var normalized=ScreenCoordinateService.ToAbsoluteMousePoint(point.X,point.Y,desktop);
        var input=new Input{Mouse=new MouseInput{Dx=normalized.X,Dy=normalized.Y,Flags=Move|Absolute|VirtualDesk|button,ExtraInfo=new UIntPtr(InputMarker)}};
        return SendInput(1,[input],Marshal.SizeOf<Input>())==1;
    }

    private static bool InjectWheel(NativePoint point,uint mouseData)
    {
        var desktop=new ScreenRect(GetSystemMetrics(76),GetSystemMetrics(77),GetSystemMetrics(78),GetSystemMetrics(79));
        var normalized=ScreenCoordinateService.ToAbsoluteMousePoint(point.X,point.Y,desktop);
        var input=new Input{Mouse=new MouseInput{Dx=normalized.X,Dy=normalized.Y,MouseData=mouseData,Flags=Move|Absolute|VirtualDesk|Wheel,ExtraInfo=new UIntPtr(InputMarker)}};
        return SendInput(1,[input],Marshal.SizeOf<Input>())==1;
    }

    public void Dispose()
    {
        lock(_gate)
        {
            if(_disposed)return;
            _disposed=true;
            if(_activeUp!=0)Inject(CurrentCursor(),_activeUp);
            _activeUp=0;
            NativeMethods.TrySetWindowMouseTransparent(_window,false);
            _dispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
        }
    }

    [StructLayout(LayoutKind.Sequential)]private struct NativePoint{internal int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseHookData{internal NativePoint Point;internal uint MouseData,Flags,Time;internal UIntPtr ExtraInfo;}
    [StructLayout(LayoutKind.Sequential)]private struct Input{internal uint Type;internal MouseInput Mouse;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseInput{internal int Dx,Dy;internal uint MouseData,Flags,Time;internal UIntPtr ExtraInfo;}
    private delegate IntPtr HookProcedure(int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int kind,HookProcedure callback,IntPtr module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? module);
    [DllImport("user32.dll")]private static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("user32.dll")]private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")]private static extern int GetSystemMetrics(int index);
}
