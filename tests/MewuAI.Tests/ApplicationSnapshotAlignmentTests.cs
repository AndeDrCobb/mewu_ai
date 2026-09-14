// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows.Media;
using System.Windows.Media.Imaging;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

public sealed class ApplicationSnapshotAlignmentTests
{
    [Theory]
    [InlineData(139, false)]
    [InlineData(139, true)]
    [InlineData(241, true)]
    public void FixedTranslucentComposerAndAnimatedCornerDoNotHideMovingContent(int shift,bool subpixel)
    {
        var first=Frame(0,0,false);var second=Frame(shift,1,subpixel);
        var actual=ScrollingCaptureComposer.EstimateVerticalShift(first,second,out _,null,1,null,true);
        Assert.InRange(actual,shift-1,shift+1);
    }

    [Fact]
    public void AnimationWithoutDocumentMovementIsRejectedEvenWithAPercentHint()
    {
        Assert.Equal(0,ScrollingCaptureComposer.EstimateVerticalShift(Frame(0,0,false),Frame(0,1,false),out _,null,1,139,true));
    }

    [Fact]
    public void UnrelatedDocumentsCannotBeStitchedThroughTheirFixedControls()
    {
        Assert.Equal(0,ScrollingCaptureComposer.EstimateVerticalShift(Frame(0,0,false),Frame(2700,1,false),out _,null,1,139,true));
    }

    private static BitmapSource Frame(int offset,int animation,bool subpixel)
    {
        const int width=768,height=720;var pixels=new byte[width*height*4];
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            int Document(int row)
            {
                var line=row/43;var within=row%43;
                var glyph=(uint)(line*104729+(x/5)*7919);glyph^=glyph>>11;glyph*=2654435761;
                return x>95&&x<690&&within is >12 and <28&&(glyph&7)<3?25:250;
            }
            var value=Document(y+offset);
            if(subpixel)value=(value+Document(y+offset+1))/2;
            if(x<75)value=(y%31<3&&x>25)?110:245;
            if(y>520&&x>90&&x<700)value=y%29<3?150:(value+3*250)/4;
            if(x>620&&y<100)value=(x+y+animation*73)%255;
            var i=(y*width+x)*4;pixels[i]=pixels[i+1]=pixels[i+2]=(byte)value;pixels[i+3]=255;
        }
        var result=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);result.Freeze();return result;
    }
}
