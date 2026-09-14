// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Views;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

/// <summary>Exercises the real annotation-to-clipboard path with synthetic pixels only.</summary>
internal static class AnnotationClipboardReplay
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    internal static void Run(Application app, CaptureOverlayWindow overlay)
    {
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        overlay.Show();
        app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
        {
            var checks = new List<string>();
            string? failure = null;
            try
            {
                // This opt-in replay replaces the clipboard with synthetic data.
                // It never reads or saves the user's previous clipboard contents.
                var sentinel = Solid(7, 9, Colors.Magenta);
                Clipboard.SetImage(sentinel);
                var a = AddSelection(overlay, new Rect(80, 100, 320, 200), Colors.White);
                var b = AddSelection(overlay, new Rect(460, 100, 240, 160), Color.FromRgb(235, 243, 250));
                Invoke(overlay, "Select", 0);
                overlay.UpdateLayout();
                await Quiesce();
                AssertClipboard(sentinel, "Selecting an unannotated screenshot changed the clipboard");
                checks.Add("plain-selection-does-not-auto-copy");

                Invoke(overlay, "EnterDrawingMode");
                overlay.UpdateLayout();
                var markup = Property<InkCanvas>(a, "Markup");
                var stroke = new Stroke(new StylusPointCollection
                {
                    new StylusPoint(30, 45), new StylusPoint(75, 45), new StylusPoint(125, 85)
                }, new DrawingAttributes { Color = Colors.Red, Width = 12, Height = 12, FitToCurve = false });
                markup.Strokes.Add(stroke);
                markup.RaiseEvent(new InkCanvasStrokeCollectedEventArgs(stroke) { RoutedEvent = InkCanvas.StrokeCollectedEvent });
                overlay.UpdateLayout();
                var handDrawn = Render(overlay, a);
                Require(CountColor(handDrawn, (r, g, blue) => r > 220 && g < 50 && blue < 50) > 250,
                    "Synthetic pen stroke was not visible in the real export");
                await UntilClipboard(handDrawn, "Completing a pen stroke did not copy the annotated image");
                checks.Add("completed-pen-stroke-auto-copies-rendered-pixels");

                Invoke(overlay, "DrawUndo", overlay, new RoutedEventArgs());
                overlay.UpdateLayout();
                var clean = Render(overlay, a, false, false, false);
                await UntilClipboard(clean, "Undoing the final stroke left stale annotation pixels in the clipboard");
                Require(markup.Strokes.Count == 0, "Drawing undo did not remove the stroke");
                checks.Add("undo-final-stroke-copies-clean-image");
                Invoke(overlay, "DrawRedo", overlay, new RoutedEventArgs());
                overlay.UpdateLayout();
                await UntilClipboard(Render(overlay, a), "Redo did not update the clipboard");
                checks.Add("redo-restores-copied-annotation");

                Invoke(overlay, "AddNumberDrawingElement", a, new Point(245, 140));
                overlay.UpdateLayout();
                var numbered = Render(overlay, a);
                Require(!Pixels(numbered).SequenceEqual(Pixels(handDrawn)), "Number annotation did not change the image");
                await UntilClipboard(numbered, "Adding a number annotation did not copy it");
                checks.Add("number-annotation-auto-copies");

                Invoke(overlay, "AddTextDrawingElement", a, new Point(20, 130));
                var editor = markup.Children.OfType<TextBox>().Single();
                editor.Text = "SYNTHETIC";
                overlay.Activate();
                editor.Focus();
                overlay.UpdateLayout();
                Require(editor.IsKeyboardFocused, "The synthetic annotation text editor did not receive focus");
                var focusedBorder = editor.BorderBrush;
                var textWithoutFocusBorder = Render(overlay, a);
                Require(CountColor(textWithoutFocusBorder,
                    (r, g, blue) => r is >= 90 and <= 135 && g is >= 100 and <= 145 && blue > 210) == 0,
                    "Committed text rendering contains the blue editor focus border");
                Invoke(overlay, "TryFlushAnnotatedImageCopy");
                AssertClipboard(textWithoutFocusBorder, "Copied text contained its editing focus border");
                Require(editor.IsKeyboardFocused && ReferenceEquals(focusedBorder, editor.BorderBrush),
                    "Automatic copy changed the editor focus or failed to restore its border");
                checks.Add("text-edits-copy-without-focus-border-and-preserve-focus");
                Invoke(overlay, "ExitDrawingMode");
                overlay.UpdateLayout();
                await UntilClipboard(Render(overlay, a), "Finishing drawing did not leave the current image in the clipboard");

                ((IList)Get(overlay, "_lastSentSelections")).Add(a);
                var handle = Property<string>(a, "ReferenceHandle");
                var targets = (IList)Get(overlay, "_lastSentAnnotationTargets");
                var targetType = typeof(CaptureOverlayWindow).GetNestedType("SentAnnotationTarget", BindingFlags.NonPublic)!;
                targets.Add(Activator.CreateInstance(targetType, handle, AiAttachmentType.Image, a));
                var note = new AiAnnotation(.52, .12, .35, .32, string.Empty, ReferenceHandle: handle,
                    Kind: AiAnnotationKind.Rectangle, Style: new AiAnnotationStyle("#00C8DC", .02, 1, true));
                // An empty primitive label and owned pixels avoid OCR and desktop UIA lookup.
                var mappingTask = (Task)Invoke(overlay, "MapAnnotationsAsync", new[] { note }, CancellationToken.None)!;
                await mappingTask;
                var mapping = mappingTask.GetType().GetProperty("Result")!.GetValue(mappingTask)!;
                Invoke(overlay, "ApplyAnnotationMapping", mapping, AiAnnotationUpdateMode.Replace, false);
                overlay.UpdateLayout();
                var combined = Render(overlay, a);
                Require(CountColor(combined, (r, g, blue) => r < 30 && g > 160 && blue > 170) > 300,
                    "The AI annotation was missing from the combined export");
                Require(CountColor(combined, (r, g, blue) => r > 220 && g < 50 && blue < 50) > 250,
                    "AI annotation replacement removed manual annotation pixels");
                await UntilClipboard(combined, "Applying AI annotations did not copy AI and manual layers together");
                checks.Add("ai-mapping-auto-copies-ai-and-manual-layers");

                Invoke(overlay, "Select", 1);
                Invoke(overlay, "EnterDrawingMode");
                Invoke(overlay, "AddNumberDrawingElement", b, new Point(100, 80));
                overlay.UpdateLayout();
                Invoke(overlay, "TryFlushAnnotatedImageCopy");
                var lastImage = Render(overlay, b);
                Clipboard.SetImage(sentinel);
                // Both queue operations happen in one dispatcher turn. Neither may copy early.
                Invoke(overlay, "QueueAnnotatedImageCopy", a);
                Invoke(overlay, "QueueAnnotatedImageCopy", b);
                AssertClipboard(sentinel, "Debounced work copied synchronously instead of coalescing");
                Invoke(overlay, "TryFlushAnnotatedImageCopy");
                AssertClipboard(lastImage, "Rapid annotation changes copied the old region instead of the latest one");
                await Quiesce();
                AssertClipboard(lastImage, "An older queued annotation overwrote the most recent region");
                checks.Add("rapid-queues-copy-only-the-latest-region");

                Invoke(overlay, "QueueAnnotatedImageCopy", a);
                Clipboard.SetText("MEWU_SYNTHETIC_EXPLICIT_COPY");
                Invoke(overlay, "TryFlushAnnotatedImageCopy");
                await Quiesce();
                Require(Clipboard.GetText() == "MEWU_SYNTHETIC_EXPLICIT_COPY",
                    "An old annotation queue overwrote a newer explicit clipboard copy");
                checks.Add("new-explicit-copy-cancels-stale-annotation-queue");

                Invoke(overlay, "ExitDrawingMode");
                Invoke(overlay, "TryFlushAnnotatedImageCopy");
                Clipboard.SetImage(sentinel);
                SetPublic(b, "VideoPath", "synthetic-not-opened.mp4");
                try
                {
                    Invoke(overlay, "QueueAnnotatedImageCopy", b);
                    Invoke(overlay, "TryFlushAnnotatedImageCopy");
                    await Quiesce();
                    AssertClipboard(sentinel, "A video annotation was incorrectly copied as a still image");
                }
                finally { SetPublic(b, "VideoPath", null); }
                checks.Add("video-annotations-do-not-copy-still-images");

                Invoke(overlay, "QueueAnnotatedImageCopy", a);
                overlay.Close();
                // Observe post-close work separately from any synchronous completion behavior.
                Clipboard.SetImage(sentinel);
                await Quiesce();
                AssertClipboard(sentinel, "Closed overlay wrote a delayed image into the clipboard");
                checks.Add("close-revokes-delayed-clipboard-work");
            }
            catch (Exception ex) { failure = ex.ToString(); Environment.ExitCode = 1; }
            finally
            {
                try { overlay.Close(); } catch { }
                var path = Path.GetFullPath(".codex-build/annotation-clipboard-result.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(new { checks, failure }), new System.Text.UTF8Encoding(false));
                app.Shutdown(Environment.ExitCode);
            }
        }));
    }

    private static object AddSelection(CaptureOverlayWindow overlay, Rect bounds, Color color)
    {
        var item = Invoke(overlay, "CreateSelection", false)!;
        SetPublic(item, "Bounds", bounds);
        SetPublic(item, "CapturedImageOverride", Solid((int)bounds.Width, (int)bounds.Height, color));
        ((IList)Get(overlay, "_selections")).Add(item);
        Invoke(overlay, "UpdateSelection", item);
        return item;
    }

    private static BitmapSource Solid(int width, int height, Color color)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = color.B; pixels[index + 1] = color.G; pixels[index + 2] = color.R; pixels[index + 3] = 255;
        }
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        image.Freeze();
        return image;
    }

    private static async Task UntilClipboard(BitmapSource expected, string message)
    {
        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (DateTime.UtcNow < deadline)
        {
            var actual = Clipboard.GetImage();
            if (actual is not null && EqualPixels(expected, actual)) return;
            await Task.Delay(30);
        }
        throw new InvalidOperationException(message);
    }

    // The production timer is 180 ms. A negative assertion also allows another
    // complete dispatcher cycle after several timer intervals, not just a sleep.
    private static async Task Quiesce()
    {
        await Task.Delay(650);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static BitmapSource Render(CaptureOverlayWindow overlay, object item, bool manual = true, bool ai = true, bool translation = true)
        => (BitmapSource)Invoke(overlay, "RenderSelectionImage", item, manual, ai, translation)!;
    private static void AssertClipboard(BitmapSource expected, string message)
    {
        var actual = Clipboard.GetImage();
        Require(actual is not null && EqualPixels(expected, actual), message);
    }
    private static bool EqualPixels(BitmapSource expected, BitmapSource actual)
        => expected.PixelWidth == actual.PixelWidth && expected.PixelHeight == actual.PixelHeight && Pixels(expected).SequenceEqual(Pixels(actual));
    private static byte[] Pixels(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        // CF_DIB consumers need not preserve an opaque source's alpha channel.
        for (var index = 3; index < pixels.Length; index += 4) pixels[index] = 255;
        return pixels;
    }
    private static int CountColor(BitmapSource image, Func<byte, byte, byte, bool> matches)
    {
        var pixels = Pixels(image); var count = 0;
        for (var index = 0; index < pixels.Length; index += 4)
            if (matches(pixels[index + 2], pixels[index + 1], pixels[index])) count++;
        return count;
    }
    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name)!.GetValue(target)!;
    private static object Get(object target, string name) => target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static void SetPublic(object target, string name, object? value) => target.GetType().GetField(name)!.SetValue(target, value);
    private static object? Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private)!.Invoke(target, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
