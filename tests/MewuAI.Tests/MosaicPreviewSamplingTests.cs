// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using mewu_ai_Assistant.Views;
using Xunit;

namespace MewuAI.Tests;

public sealed class MosaicPreviewSamplingTests
{
    [Theory]
    [InlineData(0, 0, 13, 11)]
    [InlineData(1, 2, 7, 6)]
    [InlineData(10, 9, 3, 2)]
    public void PartialRebuildKeepsScreenshotAlignedBlocksAndCleanSource(int left, int top, int width, int height)
    {
        const int sourceWidth = 13, sourceHeight = 11, blockSize = 6;
        var original = new byte[sourceWidth * sourceHeight * 4];
        for (var y = 0; y < sourceHeight; y++)
        for (var x = 0; x < sourceWidth; x++)
        {
            var offset = (y * sourceWidth + x) * 4;
            original[offset] = (byte)(x * 13 + y * 2);
            original[offset + 1] = (byte)(x * 3 + y * 11);
            original[offset + 2] = (byte)(x * 5 + y * 7);
            original[offset + 3] = 255;
        }
        var source = BitmapSource.Create(sourceWidth, sourceHeight, 144, 144, PixelFormats.Bgra32, null, original, sourceWidth * 4);
        source.Freeze();
        var region = new Int32Rect(left, top, width, height);
        var full = Create(source, new Int32Rect(0, 0, sourceWidth, sourceHeight), blockSize);
        var partial = Create(source, region, blockSize);
        var actual = new byte[width * height * 4];
        var expected = new byte[actual.Length];
        partial.CopyPixels(actual, width * 4, 0);
        full.CopyPixels(region, expected, width * 4, 0);
        Assert.Equal(expected, actual);
        Assert.True(partial.IsFrozen);
        Assert.Equal(width, partial.PixelWidth);
        Assert.Equal(height, partial.PixelHeight);
        Assert.Equal(144, partial.DpiX);
        Assert.Equal(144, partial.DpiY);

        // The last partial grid cell averages only real source pixels, with no
        // transparent padding and no re-use of an earlier mosaic as its input.
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var blockLeft = (left + x) / blockSize * blockSize;
            var blockTop = (top + y) / blockSize * blockSize;
            var right = Math.Min(sourceWidth, blockLeft + blockSize);
            var bottom = Math.Min(sourceHeight, blockTop + blockSize);
            var sum = 0;
            for (var sampleY = blockTop; sampleY < bottom; sampleY++)
            for (var sampleX = blockLeft; sampleX < right; sampleX++)
                sum += original[(sampleY * sourceWidth + sampleX) * 4];
            Assert.Equal((byte)(sum / ((right - blockLeft) * (bottom - blockTop))), actual[(y * width + x) * 4]);
            Assert.Equal(255, actual[(y * width + x) * 4 + 3]);
        }
        var unchanged = new byte[original.Length];
        source.CopyPixels(unchanged, sourceWidth * 4, 0);
        Assert.Equal(original, unchanged);
    }

    private static BitmapSource Create(BitmapSource source, Int32Rect region, int blockSize) =>
        Assert.IsAssignableFrom<BitmapSource>(typeof(CaptureOverlayWindow)
            .GetMethod("CreateMosaicPixels", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [source, region, blockSize]));
}
