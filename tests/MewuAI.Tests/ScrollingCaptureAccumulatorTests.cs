// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows.Media;
using System.Windows.Media.Imaging;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

public sealed class ScrollingCaptureAccumulatorTests
{
    [Fact]
    public void HundredsOfSmallScrollsPreserveEveryRowBeyondTheOldLimit()
    {
        var state=ScrollingCaptureAccumulator.Start(Frame(0));
        for(var y=1;y<=300;y++)state=state.Append(Frame(y),1,TestContext.Current.CancellationToken)!;
        Assert.Equal(340,state.Composite.PixelHeight);
        AssertPixels(state.Composite,0);
    }

    [Fact]
    public void RetracingDoesNotGrowTheImageAndUpwardExtensionKeepsRowOrder()
    {
        var state=ScrollingCaptureAccumulator.Start(Frame(30));
        state=state.Append(Frame(50),20,TestContext.Current.CancellationToken)!;
        var captured=state.Composite;
        state=state.Append(Frame(40),-10,TestContext.Current.CancellationToken)!;
        Assert.Same(captured,state.Composite);
        state=state.Append(Frame(20),-20,TestContext.Current.CancellationToken)!;
        state=state.Append(Frame(0),-20,TestContext.Current.CancellationToken)!;
        Assert.Equal(90,state.Composite.PixelHeight);
        AssertPixels(state.Composite,0);
    }

    [Fact]
    public void JoinReplacesTheOldViewportBorderWithOverlappingContent()
    {
        var pixels=Expected(16,40,0);Array.Clear(pixels,39*64,64);
        var first=BitmapSource.Create(16,40,96,96,PixelFormats.Bgra32,null,pixels,64);first.Freeze();
        var state=ScrollingCaptureAccumulator.Start(first).Append(Frame(20),20,TestContext.Current.CancellationToken)!;
        AssertPixels(state.Composite,0);
    }

    [Fact]
    public void CancellationCannotChangePreviouslyAcceptedPixels()
    {
        var state=ScrollingCaptureAccumulator.Start(Frame(0));
        Assert.Throws<OperationCanceledException>(()=>state.Append(Frame(10),10,new CancellationToken(true)));
        AssertPixels(state.Composite,0);Assert.Equal(40,state.Composite.PixelHeight);
    }

    private static BitmapSource Frame(int top)
    {
        var pixels=Expected(16,40,top);var result=BitmapSource.Create(16,40,96,96,PixelFormats.Bgra32,null,pixels,64);result.Freeze();return result;
    }
    private static byte[] Expected(int width,int height,int top)
    {
        var pixels=new byte[width*height*4];
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {var i=(y*width+x)*4;pixels[i]=(byte)((y+top)%251);pixels[i+1]=(byte)((y+top)/251);pixels[i+2]=(byte)x;pixels[i+3]=255;}
        return pixels;
    }
    private static void AssertPixels(BitmapSource image,int top)
    {var actual=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(actual,image.PixelWidth*4,0);Assert.Equal(Expected(image.PixelWidth,image.PixelHeight,top),actual);}
}
