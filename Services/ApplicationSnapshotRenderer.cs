// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace mewu_ai_Assistant.Services;

internal static class ApplicationSnapshotRenderer
{
    internal static BitmapSource Render(ApplicationSnapshotDocument document,int preferredWidth,bool english,CancellationToken token)
    {
        if(document.Text.Length>ApplicationSnapshotService.MaximumCharacters)throw new InvalidDataException("Snapshot text exceeds capacity.");
        var width=Math.Clamp(preferredWidth,1000,1600);const int margin=40;
        var culture=CultureInfo.GetCultureInfo(english?"en-US":"zh-CN");
        var typeface=new Typeface("Segoe UI, Microsoft YaHei UI");
        FormattedText Format(string value,double size,Brush color)=>new(value,culture,FlowDirection.LeftToRight,typeface,size,color,1){MaxTextWidth=width-margin*2,Trimming=TextTrimming.None};
        var title=Format(document.Title,26,Brushes.Black);
        var subtitle=Format(english?"Application content snapshot · Text provided by the application":"应用内容快照 · 应用提供的可读取文本",16,Brushes.DimGray);
        var blocks=new List<FormattedText>();var height=margin*2+title.Height+subtitle.Height+32;
        foreach(var chunk in TextChunks(document.Text))
        {
            token.ThrowIfCancellationRequested();var block=Format(chunk,20,Brushes.Black);height+=block.Height;blocks.Add(block);
            if(!double.IsFinite(height)||Math.Ceiling(height)*width>ScrollingCaptureComposer.MaxOutputPixels)
                throw new InvalidDataException(LocalizationService.T("应用内容过大，无法放入一张清晰的快照；请缩小内容范围。","The content is too large for one readable image. Select a smaller document."));
        }
        token.ThrowIfCancellationRequested();
        var visual=new DrawingVisual();
        using(var drawing=visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,width,Math.Ceiling(height)));
            double y=margin;drawing.DrawText(title,new Point(margin,y));y+=title.Height+8;
            drawing.DrawText(subtitle,new Point(margin,y));y+=subtitle.Height+24;
            foreach(var block in blocks){token.ThrowIfCancellationRequested();drawing.DrawText(block,new Point(margin,y));y+=block.Height;}
        }
        var image=new RenderTargetBitmap(width,(int)Math.Ceiling(height),96,96,PixelFormats.Pbgra32);image.Render(visual);image.Freeze();
        token.ThrowIfCancellationRequested();return image;
    }

    // Retain every character, including blank lines and repeated paragraphs.
    // Prefer paragraph boundaries so a normal line is never split by chunking.
    internal static IEnumerable<string> TextChunks(string text)
    {
        var start=0;
        while(start<text.Length)
        {
            var count=Math.Min(8192,text.Length-start);
            if(start+count<text.Length)
            {
                var newline=text.LastIndexOf('\n',start+count-1,count);
                if(newline>=start)count=newline-start+1;
                else if(char.IsHighSurrogate(text[start+count-1]))count--;
            }
            yield return text.Substring(start,count);start+=count;
        }
    }
}
