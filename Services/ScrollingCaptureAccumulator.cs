// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace mewu_ai_Assistant.Services;

/// <summary>Retains only the merged image and the latest matching frame.</summary>
internal sealed class ScrollingCaptureAccumulator
{
    private ScrollingCaptureAccumulator(BitmapSource composite,BitmapSource lastFrame,long origin,long top)
    {Composite=composite;LastFrame=lastFrame;Origin=origin;Top=top;}

    internal BitmapSource Composite{get;}
    internal BitmapSource LastFrame{get;}
    private long Origin{get;}
    private long Top{get;}

    internal static ScrollingCaptureAccumulator Start(BitmapSource frame)
    {
        if(!frame.IsFrozen)throw new ArgumentException("Capture frame must be frozen.",nameof(frame));
        if((long)frame.PixelWidth*frame.PixelHeight>ScrollingCaptureComposer.MaxOutputPixels)
            throw new InvalidOperationException("长截图区域超过图像容量");
        return new(frame,frame,0,0);
    }

    internal ScrollingCaptureAccumulator? Append(BitmapSource frame,int shift,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(!frame.IsFrozen||frame.PixelWidth!=LastFrame.PixelWidth||frame.PixelHeight!=LastFrame.PixelHeight||shift==0||Math.Abs((long)shift)>=frame.PixelHeight)
            throw new ArgumentException("Invalid scrolling frame or displacement.",nameof(frame));
        var origin=checked(Origin+shift);var bottom=checked(Top+Composite.PixelHeight);
        var nextTop=Math.Min(Top,origin);var nextBottom=Math.Max(bottom,checked(origin+frame.PixelHeight));
        var height=nextBottom-nextTop;var width=frame.PixelWidth;
        if(height>int.MaxValue||height*width>ScrollingCaptureComposer.MaxOutputPixels)return null;
        // Returning through already captured content changes only the match anchor.
        if(nextTop==Top&&nextBottom==bottom)return new(Composite,frame,origin,Top);
        var stride=checked(width*4);var output=new byte[checked(stride*(int)height)];
        try
        {
            CopyRows(Composite,0,Composite.PixelHeight,output,checked((int)(Top-nextTop)*stride),stride);
            if(origin<Top)CopyRows(frame,0,checked((int)(Top-origin)),output,0,stride);
            if(origin+frame.PixelHeight>bottom)
            {
                // Join inside the overlapping content, not at the old frame's
                // bottom edge, which can contain a fixed border/footer.
                var first=checked((int)(bottom-origin))/2;
                CopyRows(frame,first,frame.PixelHeight-first,output,checked((int)(origin+first-nextTop)*stride),stride);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var image=BitmapSource.Create(width,(int)height,Composite.DpiX,Composite.DpiY,PixelFormats.Bgra32,null,output,stride);
            image.Freeze();return new(image,frame,origin,nextTop);
        }
        finally{Array.Clear(output);}
    }

    private static void CopyRows(BitmapSource source,int y,int height,byte[] output,int offset,int stride)
    {
        var formatted=source.Format==PixelFormats.Bgra32?source:new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
        formatted.CopyPixels(new Int32Rect(0,y,source.PixelWidth,height),output,stride,offset);
    }
}
