// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Runtime.InteropServices;
using System.Windows.Threading;
using mewu_ai_Assistant.Models;

namespace mewu_ai_Assistant.Services;

internal sealed record PointerPassThroughPolicy(bool Enabled,ScreenRect[] Excluded,long UpdatedAt)
{
    internal bool Allows(int x,int y,long now)=>Enabled&&now-UpdatedAt<250&&
        !Excluded.Any(rect=>x>=rect.X&&x<rect.X+rect.Width&&y>=rect.Y&&y<rect.Y+rect.Height);
}

// Only the secondary-button gesture is intercepted. A dedicated message pump
// releases injected buttons even if the WPF dispatcher is busy rendering.
internal sealed class RightButtonPassThrough : IDisposable
{
    // Use a 32-bit tag: Windows may truncate dwExtraInfo in the input path.
    internal const uint InputMarker=0x5242544E;
    private readonly object _gate=new();
    private readonly IntPtr _window;
    private readonly Action<long> _beginVisual,_endVisual;
    private readonly Action _failure;
    private readonly HookProcedure _procedure;
    private readonly TaskCompletionSource<bool> _started=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Dispatcher? _dispatcher;
    private IntPtr _hook;
    private PointerPassThroughPolicy _policy=new(false,[],0);
    private bool _active,_rightHeld,_mappedHeld,_ready,_disposed,_rightMapping,_mappedRight,_discardRightUp,_cancelled;
    private long _generation,_startedAt;
    private NativePoint _start,_last,_releasePoint;
    private DispatcherTimer? _watchdog;

    internal RightButtonPassThrough(IntPtr window,Action<long> beginVisual,Action<long> endVisual,Action failure)
    {_window=window;_beginVisual=beginVisual;_endVisual=endVisual;_failure=failure;_procedure=Hook;}

    internal bool IsActive{get{lock(_gate)return _active;}}
    internal bool IsCurrent(long generation){lock(_gate)return !_disposed&&_active&&_generation==generation;}
    internal (int X,int Y) StartPoint{get{lock(_gate)return (_start.X,_start.Y);}}
    internal void Update(PointerPassThroughPolicy policy)=>Volatile.Write(ref _policy,policy);

    internal Task<bool> StartAsync()
    {
        var thread=new Thread(Run){IsBackground=true,Name="Mewu pointer routing"};thread.SetApartmentState(ApartmentState.STA);thread.Start();return _started.Task;
    }

    private void Run()
    {
        _dispatcher=Dispatcher.CurrentDispatcher;
        try
        {
            _hook=SetWindowsHookEx(14,_procedure,GetModuleHandle(null),0);
            _started.TrySetResult(_hook!=IntPtr.Zero);
            if(_hook==IntPtr.Zero)return;
            _watchdog=new DispatcherTimer(TimeSpan.FromMilliseconds(100),DispatcherPriority.Send,(_,_)=>
            {
                lock(_gate)
                {
                    if(_disposed){if(!_discardRightUp||Environment.TickCount64-_startedAt>600_000)_dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);return;}
                    if(_active&&_mappedHeld&&!_rightHeld){ReleaseMappedButton();if(!_mappedHeld)_endVisual(_generation);}
                    // A stalled UI must never replay a click long after it was
                    // requested. A lost device/desktop also has a finite lease.
                    if(_active&&Environment.TickCount64-_startedAt>(_ready?600_000:1500))CancelCore();
                }
            },_dispatcher);
            Dispatcher.Run();
        }
        catch{_started.TrySetResult(false);try{_failure();}catch{}}
        finally
        {
            _watchdog?.Stop();
            lock(_gate){ReleaseMappedButton();_active=false;}
            if(_hook!=IntPtr.Zero)UnhookWindowsHookEx(_hook);_hook=IntPtr.Zero;
        }
    }

    private IntPtr Hook(int code,IntPtr message,IntPtr data)
    {
        if(code<0)return CallNextHookEx(_hook,code,message,data);
        var input=Marshal.PtrToStructure<MouseHookData>(data);
        // Own injected events must bypass the gate: SendInput can call hooks
        // while cancellation on the UI thread holds that gate.
        if(input.ExtraInfo.ToUInt64()==InputMarker)return CallNextHookEx(_hook,code,message,data);
        var kind=unchecked((uint)message.ToInt64());
        try
        {
            lock(_gate)
            {
                if(kind==0x0205&&_discardRightUp){_discardRightUp=false;return new IntPtr(1);}
                if(_disposed)return CallNextHookEx(_hook,code,message,data);
                if(kind==0x0204) // WM_RBUTTONDOWN
                {
                    if(!_active&&(!Volatile.Read(ref _policy).Allows(input.Point.X,input.Point.Y,Environment.TickCount64)||
                        (GetAsyncKeyState(1)&0x8000)!=0))
                        return CallNextHookEx(_hook,code,message,data);
                    if(_rightHeld)return new IntPtr(1);
                    if(_mappedHeld)_dispatcher!.BeginInvoke(DispatcherPriority.Send,new Action(()=>{lock(_gate)ReleaseMappedButton();}));
                    _active=true;_rightHeld=true;_ready=false;_cancelled=false;_rightMapping=(GetAsyncKeyState(0x11)&0x8000)!=0;
                    _start=_last=input.Point;_startedAt=Environment.TickCount64;_generation++;
                    _beginVisual(_generation);return new IntPtr(1);
                }
                if(!_active)return CallNextHookEx(_hook,code,message,data);
                if(kind==0x0200){_last=input.Point;if(_rightHeld)_releasePoint=input.Point;}
                if(kind==0x0205) // WM_RBUTTONUP
                {
                    _last=_releasePoint=input.Point;_rightHeld=false;
                    if(_ready)
                    {
                        var generation=_generation;
                        _dispatcher!.BeginInvoke(DispatcherPriority.Send,new Action(()=>
                        {
                            lock(_gate){if(!_active||generation!=_generation)return;ReleaseMappedButton();_endVisual(generation);}
                        }));
                    }
                    return new IntPtr(1);
                }
            }
        }
        catch{_dispatcher?.BeginInvoke(DispatcherPriority.Send,new Action(Cancel));return kind is 0x0204 or 0x0205?new IntPtr(1):CallNextHookEx(_hook,code,message,data);}
        return CallNextHookEx(_hook,code,message,data);
    }

    internal void Ready(long generation,bool succeeded)
    {
        _dispatcher?.BeginInvoke(DispatcherPriority.Send,new Action(()=>
        {
            lock(_gate)
            {
                if(_disposed||!_active||_cancelled||generation!=_generation)return;
                if(!succeeded){CancelCore();return;}
                _ready=true;
                ReleaseMappedButton();
                if(_mappedHeld){CancelCore();return;}
                _mappedHeld=true;_mappedRight=_rightMapping;_releasePoint=_last;
                if(!Inject(_start,DownFlag(_rightMapping))){_mappedHeld=false;CancelCore();_failure();return;}
                if(_start.X!=_last.X||_start.Y!=_last.Y)Inject(_last,0);
                if(!_rightHeld){ReleaseMappedButton();_endVisual(generation);}
            }
        }));
    }

    // UI restores hit testing only after the paired up has entered the input
    // queue. A second quick click invalidates the previous restoration callback.
    internal bool TryComplete(long generation,Func<bool> restore)
    {
        lock(_gate)
        {
            if(_disposed||!_active||_rightHeld||_mappedHeld||generation!=_generation)return false;
            if(!restore())return false;
            _active=false;return true;
        }
    }

    internal void Cancel(){lock(_gate)CancelCore();}
    private void CancelCore()
    {
        if(!_active)return;
        _cancelled=true;ReleaseMappedButton();_discardRightUp|=_rightHeld;_rightHeld=false;_ready=true;
        try{_endVisual(_generation);}catch{}
    }
    private void ReleaseMappedButton()
    {
        if(!_mappedHeld)return;
        // Keep ownership until an up is accepted; disposal retries on failure.
        if(Inject(_releasePoint,UpFlag(_mappedRight)))_mappedHeld=false;
        else try{_failure();}catch{}
    }
    internal static uint DownFlag(bool right)=>right?0x0008u:0x0002u;
    internal static uint UpFlag(bool right)=>right?0x0010u:0x0004u;

    // Never call WindowFromPoint from the low-level hook: it can synchronously
    // ask the WPF thread to hit-test and stall the global input queue.
    internal void ExcludeHigherWindows(List<ScreenRect> excluded)
    {
        var current=GetWindow(_window,3);
        for(var count=0;current!=IntPtr.Zero&&count<2048;count++,current=GetWindow(current,3))
            if(IsWindowVisible(current)&&(GetWindowLongPtr(current,-20).ToInt64()&0x20)==0&&
                GetWindowRect(current,out var rect)&&rect.Right>rect.Left&&rect.Bottom>rect.Top)
                excluded.Add(new(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top));
    }

    private static bool Inject(NativePoint point,uint button)
    {
        var desktop=new ScreenRect(GetSystemMetrics(76),GetSystemMetrics(77),GetSystemMetrics(78),GetSystemMetrics(79));
        var normalized=ScreenCoordinateService.ToAbsoluteMousePoint(point.X,point.Y,desktop);
        var input=new Input{Mouse=new MouseInput{Dx=normalized.X,Dy=normalized.Y,Flags=0x8000|0x4000|0x0001|button,ExtraInfo=new UIntPtr(InputMarker)}};
        return SendInput(1,[input],Marshal.SizeOf<Input>())==1;
    }

    public void Dispose()
    {
        lock(_gate){if(_disposed)return;_disposed=true;_discardRightUp|=_rightHeld;ReleaseMappedButton();_active=false;if(!_discardRightUp)_dispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);}
    }

    private delegate IntPtr HookProcedure(int code,IntPtr message,IntPtr data);
    [StructLayout(LayoutKind.Sequential)]private struct NativePoint{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseHookData{public NativePoint Point;public uint MouseData,Flags,Time;public UIntPtr ExtraInfo;}
    [StructLayout(LayoutKind.Sequential)]private struct Input{public uint Type;public MouseInput Mouse;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseInput{public int Dx,Dy;public uint MouseData,Flags,Time;public UIntPtr ExtraInfo;}
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int kind,HookProcedure callback,IntPtr module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? module);
    [DllImport("user32.dll")]private static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")]private static extern uint SendInput(uint count,Input[] inputs,int size);
}
