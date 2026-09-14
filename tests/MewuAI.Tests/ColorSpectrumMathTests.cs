// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Media;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

public sealed class ColorSpectrumMathTests
{
    [Theory]
    [InlineData(255,0,0,0)]
    [InlineData(255,255,0,60)]
    [InlineData(0,255,0,120)]
    [InlineData(0,255,255,180)]
    [InlineData(0,0,255,240)]
    [InlineData(255,0,255,300)]
    public void PrimaryAndSecondaryColorsMatchHueWheel(int red,int green,int blue,double hue)
    {
        var expected=Color.FromRgb((byte)red,(byte)green,(byte)blue);
        Assert.Equal(new HsvColor(hue,1,1),ColorSpectrumMath.FromRgb(expected,27));
        Assert.Equal(expected,ColorSpectrumMath.ToRgb(new HsvColor(hue,1,1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(255)]
    public void AchromaticColorsKeepTheUsersLastHue(int brightness)
    {
        var gray=Color.FromRgb((byte)brightness,(byte)brightness,(byte)brightness);
        var hsv=ColorSpectrumMath.FromRgb(gray,231.25);
        Assert.Equal(231.25,hsv.Hue);
        Assert.Equal(0,hsv.Saturation);
        Assert.Equal(brightness/255d,hsv.Value);
        Assert.Equal(gray,ColorSpectrumMath.ToRgb(hsv));
        Assert.Equal(330,ColorSpectrumMath.FromRgb(gray,-30).Hue);
        Assert.Equal(0,ColorSpectrumMath.FromRgb(gray,double.NaN).Hue);
    }

    [Fact]
    public void NonPrimaryColorsHaveCorrectSaturationAndValue()
    {
        var hsv=ColorSpectrumMath.FromRgb(Color.FromRgb(128,64,32));
        Assert.Equal(20,hsv.Hue,10);
        Assert.Equal(.75,hsv.Saturation,10);
        Assert.Equal(128/255d,hsv.Value,10);
        Assert.Equal(Color.FromRgb(128,64,32),ColorSpectrumMath.ToRgb(hsv));
    }

    [Fact]
    public void RgbRoundTripsStayWithinOneByteAcrossTheColorCube()
    {
        byte[] samples=[0,1,2,17,32,64,127,128,191,254,255];
        foreach(var red in samples)
        foreach(var green in samples)
        foreach(var blue in samples)
        {
            var original=Color.FromRgb(red,green,blue);
            var hsv=ColorSpectrumMath.FromRgb(original,217);
            var restored=ColorSpectrumMath.ToRgb(hsv);
            Assert.InRange(hsv.Hue,0,359.999999999);
            Assert.InRange(hsv.Saturation,0,1);
            Assert.InRange(hsv.Value,0,1);
            Assert.InRange(Math.Abs(restored.R-red),0,1);
            Assert.InRange(Math.Abs(restored.G-green),0,1);
            Assert.InRange(Math.Abs(restored.B-blue),0,1);
            Assert.Equal(255,restored.A);
        }
    }

    [Theory]
    [InlineData(360,255,0,0)]
    [InlineData(720,255,0,0)]
    [InlineData(-360,255,0,0)]
    [InlineData(-60,255,0,255)]
    [InlineData(420,255,255,0)]
    [InlineData(double.NaN,255,0,0)]
    [InlineData(double.PositiveInfinity,255,0,0)]
    public void HueWrapsWithoutDiscontinuityOrInvalidColor(double hue,int red,int green,int blue)
    {
        Assert.Equal(Color.FromRgb((byte)red,(byte)green,(byte)blue),ColorSpectrumMath.ToRgb(new HsvColor(hue,1,1)));
    }

    [Theory]
    [InlineData(2,2,255,0,0)]
    [InlineData(-1,1,255,255,255)]
    [InlineData(1,-1,0,0,0)]
    [InlineData(double.NaN,1,255,255,255)]
    [InlineData(1,double.NaN,0,0,0)]
    [InlineData(double.PositiveInfinity,double.PositiveInfinity,255,0,0)]
    [InlineData(double.NegativeInfinity,1,255,255,255)]
    public void SaturationAndValueAreClampedToValidRgb(double saturation,double value,int red,int green,int blue)
    {
        Assert.Equal(Color.FromRgb((byte)red,(byte)green,(byte)blue),ColorSpectrumMath.ToRgb(new HsvColor(0,saturation,value)));
    }

    [Theory]
    [InlineData(1,0,0)]
    [InlineData(1,1,45)]
    [InlineData(0,1,90)]
    [InlineData(-1,1,135)]
    [InlineData(-1,0,180)]
    [InlineData(-1,-1,225)]
    [InlineData(0,-1,270)]
    [InlineData(1,-1,315)]
    public void PointerHueUsesClockwiseScreenAngles(double x,double y,double expectedHue)
    {
        var center=new Point(25,60);
        Assert.Equal(expectedHue,ColorSpectrumMath.HueAtPoint(new Point(center.X+x,center.Y+y),center),10);
    }

    [Fact]
    public void PointerHueIsFiniteAtCenterAndForInvalidCoordinates()
    {
        var center=new Point(25,60);
        Assert.Equal(0,ColorSpectrumMath.HueAtPoint(center,center));
        Assert.Equal(0,ColorSpectrumMath.HueAtPoint(new Point(double.NaN,60),center));
        Assert.Equal(0,ColorSpectrumMath.HueAtPoint(new Point(25,double.PositiveInfinity),center));
        Assert.Equal(0,ColorSpectrumMath.HueAtPoint(new Point(),new Point(double.NegativeInfinity,60)));
        Assert.Equal(0,ColorSpectrumMath.FromRgb(Colors.Gray,-double.Epsilon).Hue);
    }

    [Theory]
    [InlineData(0,0,0,1)]
    [InlineData(137.5,0,1,1)]
    [InlineData(0,83.25,0,0)]
    [InlineData(137.5,83.25,1,0)]
    [InlineData(34.375,62.4375,.25,.25)]
    [InlineData(-100,-100,0,1)]
    [InlineData(500,500,1,0)]
    public void SaturationValueBoardUsesItsExactSizeAndClipsAtEdges(double x,double y,double saturation,double value)
    {
        Assert.Equal(new Point(saturation,value),ColorSpectrumMath.ClampSaturationValue(new Point(x,y),new Size(137.5,83.25)));
    }

    [Fact]
    public void InvalidBoardSizeOrPointerDoesNotProduceNaN()
    {
        foreach(var size in new[]{Size.Empty,new Size(),new Size(0,100),new Size(100,0),new Size(double.NaN,100),new Size(100,double.PositiveInfinity)})
            Assert.Equal(new Point(),ColorSpectrumMath.ClampSaturationValue(new Point(50,50),size));
        Assert.Equal(new Point(),ColorSpectrumMath.ClampSaturationValue(new Point(double.NaN,50),new Size(100,100)));
        Assert.Equal(new Point(),ColorSpectrumMath.ClampSaturationValue(new Point(50,double.NegativeInfinity),new Size(100,100)));
    }
}
