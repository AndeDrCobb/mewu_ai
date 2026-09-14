// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Graphics.Canvas;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using WinRT;
using mewu_ai_Assistant.Models;

namespace mewu_ai_Assistant.Services;

// Read the selected HWND's compositor surface, independently of windows above
// it. No screen BitBlt, capture holes, mouse wheel injection or image encoding.
internal sealed class ApplicationWindowCapture : IDisposable
{
    private readonly CanvasDevice _device;
    private readonly Direct3D11CaptureFramePool _pool;
    private readonly GraphicsCaptureSession _session;
    private readonly object _gate=new();
    private BitmapSource? _latest;
    private Exception? _failure;
    private bool _disposed;
    private long _generation;
    internal ScreenRect Bounds{get;}

    internal ApplicationWindowCapture(ApplicationSnapshotTarget target)
    {
        if(!target.IsCurrent()||!GraphicsCaptureSession.IsSupported())throw new NotSupportedException("Window capture is unavailable.");
        var item=GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>().CreateForWindow(new IntPtr(target.Handle),new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760"));
        GraphicsCaptureItem captureItem;
        try{captureItem=MarshalInterface<GraphicsCaptureItem>.FromAbi(item);}
        finally{Marshal.Release(item);}
        if(captureItem.Size.Width<=0||captureItem.Size.Height<=0||(long)captureItem.Size.Width*captureItem.Size.Height>16_000_000)
            throw new InvalidDataException("Window surface is too large.");
        Marshal.ThrowExceptionForHR(DwmGetWindowAttribute(new IntPtr(target.Handle),9,out var bounds,Marshal.SizeOf<NativeRect>()));
        Bounds=new(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top);
        if(Bounds.Width!=captureItem.Size.Width||Bounds.Height!=captureItem.Size.Height)throw new InvalidDataException("Window surface coordinates changed.");
        _device=new CanvasDevice();
        try
        {
            _pool=Direct3D11CaptureFramePool.CreateFreeThreaded(_device,DirectXPixelFormat.B8G8R8A8UIntNormalized,2,captureItem.Size);
            _session=_pool.CreateCaptureSession(captureItem);
            _session.IsCursorCaptureEnabled=false;
            _pool.FrameArrived+=OnFrame;
            _session.StartCapture();
        }
        catch{_session?.Dispose();_pool?.Dispose();_device.Dispose();throw;}
    }

    internal long Generation{get{lock(_gate)return _generation;}}
    internal BitmapSource? Latest{get{lock(_gate){if(_failure is not null)throw new IOException("Window rendering failed.",_failure);return _latest;}}}

    private void OnFrame(Direct3D11CaptureFramePool sender,object args)
    {
        lock(_gate)
        {
            if(_disposed)return;
            try
            {
                using var frame=sender.TryGetNextFrame();if(frame is null)return;
                using var deviceLock=_device.Lock();
                using var bitmap=CanvasBitmap.CreateFromDirect3D11Surface(_device,frame.Surface);
                var width=frame.ContentSize.Width;var height=frame.ContentSize.Height;
                if(width<=0||height<=0||width!=(int)bitmap.SizeInPixels.Width||height!=(int)bitmap.SizeInPixels.Height)
                    throw new InvalidDataException("Window resized during capture.");
                var pixels=bitmap.GetPixelBytes();
                try{var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,checked(width*4));image.Freeze();_latest=image;_generation++;}
                finally{CryptographicOperations.ZeroMemory(pixels);}
            }
            catch(Exception ex){_failure=ex;}
        }
    }

    public void Dispose()
    {
        lock(_gate){if(_disposed)return;_disposed=true;_latest=null;}
        _pool.FrameArrived-=OnFrame;_session.Dispose();_pool.Dispose();_device.Dispose();
    }

    [ComImport,Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window,[In]ref Guid iid);
        IntPtr CreateForMonitor(IntPtr monitor,[In]ref Guid iid);
    }
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{public int Left,Top,Right,Bottom;}
    [DllImport("dwmapi.dll")]private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out NativeRect value,int size);
}
