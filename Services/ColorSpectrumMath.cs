// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Media;

namespace mewu_ai_Assistant.Services;

internal readonly record struct HsvColor(double Hue,double Saturation,double Value);

/// <summary>RGB/HSV and pointer coordinates for the local, opaque RGB color picker.</summary>
internal static class ColorSpectrumMath
{
    internal static HsvColor FromRgb(Color color,double fallbackHue=0)
    {
        var red=color.R/255d;var green=color.G/255d;var blue=color.B/255d;
        var maximum=Math.Max(red,Math.Max(green,blue));
        var minimum=Math.Min(red,Math.Min(green,blue));
        var chroma=maximum-minimum;
        // Gray and black do not define a hue. Keep the user's last hue so that
        // increasing saturation or value does not unexpectedly jump back to red.
        if(chroma==0)return new HsvColor(NormalizeHue(fallbackHue),0,maximum);
        var sector=maximum==red?(green-blue)/chroma:
            maximum==green?(blue-red)/chroma+2:(red-green)/chroma+4;
        return new HsvColor(NormalizeHue(sector*60),chroma/maximum,maximum);
    }

    internal static Color ToRgb(HsvColor hsv)
    {
        var hue=NormalizeHue(hsv.Hue)/60;
        var value=ClampUnit(hsv.Value);
        var chroma=value*ClampUnit(hsv.Saturation);
        var secondary=chroma*(1-Math.Abs(hue%2-1));
        var offset=value-chroma;
        var (red,green,blue)=hue switch
        {
            <1=>(chroma,secondary,0d),
            <2=>(secondary,chroma,0d),
            <3=>(0d,chroma,secondary),
            <4=>(0d,secondary,chroma),
            <5=>(secondary,0d,chroma),
            _=>(chroma,0d,secondary)
        };
        return Color.FromRgb(ToByte(red+offset),ToByte(green+offset),ToByte(blue+offset));
    }

    internal static double HueAtPoint(Point pointer,Point center)
    {
        if(!FinitePoint(pointer)||!FinitePoint(center))return 0;
        // WPF screen Y increases downward, so this intentionally runs clockwise.
        return NormalizeHue(Math.Atan2(pointer.Y-center.Y,pointer.X-center.X)*180/Math.PI);
    }

    internal static Point ClampSaturationValue(Point pointer,Size size)
    {
        if(size.IsEmpty||!double.IsFinite(size.Width)||!double.IsFinite(size.Height)||
            size.Width<=0||size.Height<=0||!FinitePoint(pointer))return new Point();
        return new Point(ClampUnit(pointer.X/size.Width),ClampUnit(1-pointer.Y/size.Height));
    }

    private static double NormalizeHue(double hue)
    {
        if(!double.IsFinite(hue))return 0;
        var normalized=hue%360;
        if(normalized<0)normalized+=360;
        return normalized>=360?0:normalized;
    }

    private static double ClampUnit(double value)=>double.IsNaN(value)?0:Math.Clamp(value,0,1);
    private static bool FinitePoint(Point point)=>double.IsFinite(point.X)&&double.IsFinite(point.Y);
    private static byte ToByte(double value)=>(byte)Math.Clamp(Math.Round(value*255,MidpointRounding.AwayFromZero),0,255);
}
