// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace mewu_ai_Assistant.Services;

internal static class PinnedWindowSizeHook
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public Point Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize; }
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,ref MinMaxInfo info);

    internal static void Attach(IntPtr handle)
    {
        HwndSource.FromHwnd(handle)?.AddHook(HandleMessage);
        // Refresh WPF's cached native tracking bounds after SourceInitialized.
        var info=new MinMaxInfo();SendMessage(handle,0x0024,IntPtr.Zero,ref info);
    }

    private static IntPtr HandleMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message!=0x0024)return IntPtr.Zero;
        var info=Marshal.PtrToStructure<MinMaxInfo>(lParam);
        info.MaxTrackSize=new Point{X=(int)PinnedWindowZoomPolicy.MaxWindowDimension,Y=(int)PinnedWindowZoomPolicy.MaxWindowDimension};
        info.MinTrackSize=new Point{X=1,Y=1};
        Marshal.StructureToPtr(info,lParam,false);
        // WPF must consume these values too, otherwise its layout remains
        // constrained to the monitor even when the HWND is larger.
        handled=false;return IntPtr.Zero;
    }
}
