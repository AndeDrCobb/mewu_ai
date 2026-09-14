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
using mewu_ai_Assistant.Views;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using ComboBox = System.Windows.Controls.ComboBox;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

/// <summary>Opt-in replay of actual manual annotation editors on synthetic images.</summary>
internal static class ManualDrawingReplay
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

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
                var item = AddSelection(overlay, new Rect(65, 100, 520, 330));
                var markup = Property<InkCanvas>(item, "Markup");
                var elements = Property<IList>(item, "DrawingElements");
                Invoke(overlay, "Select", 0);
                Invoke(overlay, "EnterDrawingMode");
                overlay.Activate();
                overlay.UpdateLayout();

                // Selecting a finished pen stroke and changing its color should
                // edit that stroke, not just the color of the next stroke.
                var stroke = new Stroke(new StylusPointCollection
                {
                    new StylusPoint(40, 60), new StylusPoint(190, 60), new StylusPoint(220, 95)
                }, new DrawingAttributes { Color = Colors.Red, Width = 10, Height = 10, FitToCurve = false });
                markup.Strokes.Add(stroke);
                markup.RaiseEvent(new InkCanvasStrokeCollectedEventArgs(stroke) { RoutedEvent = InkCanvas.StrokeCollectedEvent });
                var redPen = Render(overlay, item);
                SelectDrawingObject(overlay, item, new Point(100, 60));
                FocusControl(overlay, "DrawingPenButton");
                Invoke(overlay, "SetDrawColor", Colors.Blue);
                Require(stroke.DrawingAttributes.Color == Colors.Blue, "Changing the selected pen color only changed the next pen");
                Require(CountColor(Render(overlay, item), Blue) > 500, "The selected pen's changed color did not reach exported pixels");
                Undo(overlay);
                Require(stroke.DrawingAttributes.Color == Colors.Red && SamePixels(redPen, Render(overlay, item)), "Undo did not restore the pen color and pixels");
                Redo(overlay);
                Require(stroke.DrawingAttributes.Color == Colors.Blue, "Redo did not restore the changed pen color");
                checks.Add("selected-pen-color-and-undo-redo-change-real-pixels");

                Clear(overlay);
                Invoke(overlay, "SetDrawColor", Colors.Red);
                Invoke(overlay, "DrawNumberTool", overlay, new RoutedEventArgs());
                foreach (var point in new[] { new Point(65, 65), new Point(145, 65), new Point(225, 65) })
                    Invoke(overlay, "AddNumberDrawingElement", item, point);
                Require(Numbers(elements).SequenceEqual(new[] { 1, 2, 3 }), "Number placement did not start with 1, 2, 3");
                Require(elements.Cast<object>().All(element => Property<double>(element, "Diameter") is >= 22 and <= 28),
                    "Default number markers are not compact");
                var second = elements.Cast<object>().Single(element => Property<int>(element, "Number") == 2);
                SelectDrawingObject(overlay, item, Center(second));
                Require((bool)Invoke(overlay, "DeleteSelectedDrawingObject")!, "The selected number could not be removed");
                Invoke(overlay, "AddNumberDrawingElement", item, new Point(305, 65));
                Require(Numbers(elements).SequenceEqual(new[] { 1, 3, 2 }), "Placing a new number did not reuse the smallest unused number");
                Require(Numbers(elements).Distinct().Count() == elements.Count, "Reusing a number created duplicate labels");
                Undo(overlay);
                Require(Numbers(elements).SequenceEqual(new[] { 1, 3 }), "Undo did not remove the newly reused number");
                Redo(overlay);
                Require(Numbers(elements).SequenceEqual(new[] { 1, 3, 2 }), "Redo did not preserve the reused number's identity");
                checks.Add("compact-number-markers-reuse-gaps-and-survive-undo-redo");

                var firstNumber = elements.Cast<object>().Single(element => Property<int>(element, "Number") == 1);
                var numberId = Property<Guid>(firstNumber, "Id");
                SelectDrawingObject(overlay, item, Center(firstNumber));
                FocusControl(overlay, "DrawingNumberButton");
                Invoke(overlay, "SetDrawColor", Colors.LimeGreen);
                Require(Property<Color>(ById(elements, numberId), "Color") == Colors.LimeGreen, "Selected number ignored color changes after focus moved to the toolbar");
                Require(CountColor(Render(overlay, item), (r, g, b) => r < 80 && g > 150 && b < 100) > 100,
                    "Changed number color was missing from exported pixels");
                Undo(overlay);
                Require(Property<Color>(ById(elements, numberId), "Color") == Colors.Red, "Undo did not restore the number color");
                Redo(overlay);
                Require(Property<Color>(ById(elements, numberId), "Color") == Colors.LimeGreen, "Redo did not restore the number color");
                checks.Add("selected-number-color-persists-with-toolbar-focus-and-undo-redo");

                Clear(overlay);
                Invoke(overlay, "SetDrawColor", Colors.Red);
                Invoke(overlay, "DrawTextTool", overlay, new RoutedEventArgs());
                Invoke(overlay, "AddTextDrawingElement", item, new Point(45, 75));
                var editor = markup.Children.OfType<TextBox>().Single();
                editor.Text = "Synthetic text";
                var textId = (Guid)editor.Tag;
                var originalSize = Property<double>(ById(elements, textId), "FontSize");
                var originalFamily = Property<string>(ById(elements, textId), "FontFamily");
                SelectDrawingObject(overlay, item, new Point(53, 83));
                var sizes = (ComboBox)overlay.FindName("DrawingFontSize");
                sizes.Focus();
                Require(!editor.IsKeyboardFocusWithin, "Text editor retained focus when selecting a toolbar size");
                sizes.SelectedItem = 40d;
                Require(Property<double>(ById(elements, textId), "FontSize") == 40, "Selected text did not adopt the toolbar font size");
                var resizedText = Render(overlay, item);
                Undo(overlay);
                Require(Property<double>(ById(elements, textId), "FontSize") == originalSize, "Undo did not restore the text size");
                Require(!SamePixels(resizedText, Render(overlay, item)), "Text-size undo changed no exported pixels");
                Redo(overlay);
                Require(Property<double>(ById(elements, textId), "FontSize") == 40, "Redo did not restore the text size");
                checks.Add("selected-text-size-edits-survive-focus-change-and-undo-redo");

                SelectDrawingObject(overlay, item, new Point(53, 83));
                var fonts = (ComboBox)overlay.FindName("DrawingFontFamily");
                var selectedFamily = fonts.Items.Cast<object>().FirstOrDefault(choice =>
                    Property<string>(choice, "Source").Equals("Arial", StringComparison.OrdinalIgnoreCase) &&
                    !Property<string>(choice, "Source").Equals(originalFamily, StringComparison.OrdinalIgnoreCase))
                    ?? fonts.Items.Cast<object>().First(choice => !Property<string>(choice, "Source").Equals(originalFamily, StringComparison.OrdinalIgnoreCase));
                var changedFamily = Property<string>(selectedFamily, "Source");
                fonts.Focus();
                fonts.SelectedItem = selectedFamily;
                Require(Property<string>(ById(elements, textId), "FontFamily") == changedFamily, "Selected text ignored the toolbar font family");
                Undo(overlay);
                Require(Property<string>(ById(elements, textId), "FontFamily") == originalFamily, "Undo did not restore the text font family");
                Redo(overlay);
                Require(Property<string>(ById(elements, textId), "FontFamily") == changedFamily, "Redo did not restore the text font family");
                checks.Add("selected-text-font-family-edits-survive-focus-change-and-undo-redo");

                SelectDrawingObject(overlay, item, new Point(53, 83));
                FocusControl(overlay, "DrawingTextButton");
                Invoke(overlay, "SetDrawColor", Colors.Blue);
                Require(Property<Color>(ById(elements, textId), "Color") == Colors.Blue && CountColor(Render(overlay, item), Blue) > 100,
                    "Changing selected text color failed after focus moved to the toolbar");
                Undo(overlay);
                Require(Property<Color>(ById(elements, textId), "Color") == Colors.Red, "Undo did not restore text color");
                Redo(overlay);
                Require(Property<Color>(ById(elements, textId), "Color") == Colors.Blue, "Redo did not restore text color");
                Require(Property<string>(ById(elements, textId), "Text") == "Synthetic text", "Text styling changed the annotation's content");
                checks.Add("selected-text-color-is-undoable-and-preserves-content");

                var continuedEditor = markup.Children.OfType<TextBox>().Single();
                continuedEditor.Text = "abc";
                SelectDrawingObject(overlay, item, new Point(53, 83));
                var sizeBeforeContinuedTyping = Property<double>(ById(elements, textId), "FontSize");
                sizes.Focus();
                sizes.SelectedItem = 28d;
                Require(Property<double>(ById(elements, textId), "FontSize") == 28, "The continued-typing fixture did not change text size");
                continuedEditor = markup.Children.OfType<TextBox>().Single();
                continuedEditor.Focus();
                continuedEditor.AppendText("d");
                FocusControl(overlay, "DrawingTextButton");
                Undo(overlay);
                Require(Property<string>(ById(elements, textId), "Text") == "abcd" &&
                    Property<double>(ById(elements, textId), "FontSize") == sizeBeforeContinuedTyping,
                    "Undoing a text-style change discarded text typed after that change");
                Redo(overlay);
                Require(Property<string>(ById(elements, textId), "Text") == "abcd" &&
                    Property<double>(ById(elements, textId), "FontSize") == 28,
                    "Redoing a text-style change discarded text typed after that change");
                checks.Add("text-style-undo-redo-preserves-later-typed-content");

                foreach (var selector in new[] { sizes, fonts })
                {
                    selector.IsDropDownOpen = true;
                    overlay.UpdateLayout();
                    if (selector.ItemContainerGenerator.ContainerFromIndex(0) is ComboBoxItem choice) choice.Focus();
                    else selector.Focus();
                    var enter = new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(overlay)!, Environment.TickCount, Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = selector };
                    Invoke(overlay, "OnPreviewKeyDown", overlay, enter);
                    Require(!(bool)Get(overlay, "_closed") && !enter.Handled,
                        "Enter in a drawing font dropdown was consumed by screenshot completion");
                    selector.IsDropDownOpen = false;
                }
                checks.Add("enter-in-font-size-and-family-dropdowns-keeps-overlay-open");

                Clear(overlay);
                Invoke(overlay, "DrawTextTool", overlay, new RoutedEventArgs());
                Invoke(overlay, "AddTextDrawingElement", item, new Point(45, 75));
                Require(elements.Count == 1, "An empty text editor was not created");
                var blankEditor = markup.Children.OfType<TextBox>().Single();
                sizes.IsDropDownOpen = true;
                overlay.UpdateLayout();
                if (sizes.ItemContainerGenerator.ContainerFromIndex(0) is ComboBoxItem sizeChoice) sizeChoice.Focus();
                else sizes.Focus();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Require(elements.Count == 1 && markup.Children.Contains(blankEditor), "Opening the font-size popup deleted the empty text draft");
                sizes.IsDropDownOpen = false;
                blankEditor.Focus();
                checks.Add("empty-text-draft-survives-font-size-popup-focus");
                ((Canvas)overlay.FindName("Root")).Focus();
                await Until(() => elements.Count == 0, "An empty text editor remained after losing focus");
                Require(markup.Children.OfType<TextBox>().Count() == 0, "Empty text cleanup left an orphan editor on the canvas");
                checks.Add("empty-text-editor-is-removed-on-focus-loss");

                Invoke(overlay, "DrawTextTool", overlay, new RoutedEventArgs());
                Invoke(overlay, "AddTextDrawingElement", item, new Point(45, 75));
                Invoke(overlay, "DrawNumberTool", overlay, new RoutedEventArgs());
                await Until(() => elements.Count == 0, "Switching tools left an empty text annotation");
                checks.Add("switching-drawing-tools-cleans-empty-text");
                Invoke(overlay, "DrawTextTool", overlay, new RoutedEventArgs());
                Invoke(overlay, "AddTextDrawingElement", item, new Point(45, 75));
                Invoke(overlay, "ExitDrawingMode");
                await Until(() => elements.Count == 0, "Completing drawing left an empty text annotation");
                checks.Add("finishing-drawing-cleans-empty-text");
                Invoke(overlay, "EnterDrawingMode");

                VerifyLine(overlay, item, markup, checks);
                VerifyMosaic(overlay, item, markup, checks);
            }
            catch (Exception ex) { failure = ex.ToString(); Environment.ExitCode = 1; }
            finally
            {
                try { overlay.Close(); } catch { }
                var path = Path.GetFullPath(".codex-build/manual-drawing-result.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(new { checks, failure }), new System.Text.UTF8Encoding(false));
                app.Shutdown(Environment.ExitCode);
            }
        }));
    }

    private static void VerifyLine(CaptureOverlayWindow overlay, object item, InkCanvas markup, List<string> checks)
    {
        Clear(overlay);
        Invoke(overlay, "SetDrawColor", Colors.Red);
        Invoke(overlay, "DrawLineTool", overlay, new RoutedEventArgs());
        var tool = Get(overlay, "_drawTool");
        Require(tool.ToString() == "Line", "The line tool did not select a distinct line mode");
        var start = new Point(60, 70); var end = new Point(360, 135);
        var line = (Stroke)typeof(CaptureOverlayWindow).GetMethod("CreateShapeStroke", StaticPrivate)!
            .Invoke(null, new[] { (object)markup, start, end, tool, false })!;
        Set(overlay, "_drawStart", start);
        Set(overlay, "_drawPreview", line);
        markup.Strokes.Add(line);
        markup.CaptureMouse();
        Invoke(overlay, "MarkupUp", markup, new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Source = markup });
        Require(markup.Strokes.Count == 1 && line.StylusPoints.Count == 2, "Completing a line created an arrow or a multi-segment stroke");
        Require(!line.DrawingAttributes.FitToCurve, "A straight line was converted to a fitted curve");
        var handles = (IList)Get(overlay, "_drawingObjectHandles");
        Require(handles.Count == 2, "A selected line did not expose two independent endpoints");
        Require(CountColor(Render(overlay, item), Red) > 500, "The line is absent from exported pixels");
        checks.Add("line-tool-commits-a-visible-two-endpoint-stroke");

        var moved = new Point(80, 200);
        Require((bool)Invoke(overlay, "TryBeginDrawingResize", item, start, markup)!, "The first line endpoint could not be grabbed");
        Invoke(overlay, "ResizeSelectedDrawingObject", item, moved, markup);
        Invoke(overlay, "CommitSelectedDrawingMove");
        markup.ReleaseMouseCapture();
        Require(PointOf(line.StylusPoints[0]) == moved && PointOf(line.StylusPoints[1]) == end, "Moving one line endpoint moved the other endpoint");
        Undo(overlay);
        Require(PointOf(line.StylusPoints[0]) == start && PointOf(line.StylusPoints[1]) == end, "Undo did not restore line endpoints");
        Redo(overlay);
        Require(PointOf(line.StylusPoints[0]) == moved && PointOf(line.StylusPoints[1]) == end, "Redo did not restore the changed line endpoint");
        checks.Add("line-endpoints-edit-independently-and-support-undo-redo");
    }

    private static void VerifyMosaic(CaptureOverlayWindow overlay, object item, InkCanvas markup, List<string> checks)
    {
        Clear(overlay);
        var elements = Property<IList>(item, "DrawingElements");
        var source = SyntheticBitmap(520, 330, patterned: true);
        SetPublic(item, "CapturedImageOverride", source);
        Invoke(overlay, "UpdateSelection", item);
        Invoke(overlay, "DrawMosaicTool", overlay, new RoutedEventArgs());
        var start = new Point(60, 60); var end = new Point(240, 170);
        Set(overlay, "_drawStart", start);
        Invoke(overlay, "BeginMosaicDrawingPreview", item);
        var previewImage = markup.Children.OfType<Image>().Single();
        var previewSource = previewImage.Source;
        Invoke(overlay, "UpdateMosaicDrawingPreview", item, new Point(150, 130));
        Invoke(overlay, "UpdateMosaicDrawingPreview", item, end);
        overlay.UpdateLayout();
        Require(ReferenceEquals(previewSource, previewImage.Source), "Mosaic dragging repeatedly rebuilt its source image");
        Require(elements.Count == 0 && markup.Strokes.Count == 0 && Property<IList>(item, "DrawingOrder").Count == 0,
            "Dragging mosaic prematurely committed an annotation, history item or fake outline stroke");
        Require(markup.Children.OfType<Image>().Any(), "Dragging mosaic shows no pixelated image preview");
        var preview = new RenderTargetBitmap(520, 330, 96, 96, PixelFormats.Pbgra32);
        preview.Render(markup);
        var region = new Int32Rect(80, 80, 60, 50);
        var cleanColors = DistinctColors(source, region);
        var previewColors = DistinctColors(preview, region);
        Require(previewColors >= 4 && previewColors < cleanColors / 4, "The mosaic drag preview is not a multi-color pixelated version of the source");
        checks.Add("mosaic-drag-shows-real-pixelated-preview-before-commit");

        Invoke(overlay, "CommitMosaicDrawingPreview", item, end);
        overlay.UpdateLayout();
        Require(elements.Count == 1 && elements[0]!.GetType().Name == "MosaicDrawingElement", "Completing mosaic did not create one persistent mosaic element");
        var committed = Render(overlay, item);
        Require(!SamePixels(source, committed), "Committed mosaic did not change exported pixels");
        Require(DistinctColors(committed, region) < cleanColors / 4, "Exported mosaic is not pixelated");
        Undo(overlay);
        Require(elements.Count == 0 && SamePixels(source, Render(overlay, item)), "Undoing mosaic did not restore the exact source image");
        Redo(overlay);
        Require(elements.Count == 1 && SamePixels(committed, Render(overlay, item)), "Redo did not reproduce the committed mosaic pixels");
        checks.Add("mosaic-commit-and-undo-redo-preserve-exact-source-and-result");

        Clear(overlay);
        Set(overlay, "_drawStart", start);
        Invoke(overlay, "BeginMosaicDrawingPreview", item);
        Invoke(overlay, "UpdateMosaicDrawingPreview", item, end);
        Invoke(overlay, "CancelMosaicDrawingPreview");
        Require(elements.Count == 0 && !markup.Children.OfType<Image>().Any() && SamePixels(source, Render(overlay, item)),
            "Canceling a mosaic drag left preview pixels or an annotation behind");
        checks.Add("canceling-mosaic-removes-preview-without-changing-source");
    }

    private static object AddSelection(CaptureOverlayWindow overlay, Rect bounds)
    {
        var item = Invoke(overlay, "CreateSelection", false)!;
        SetPublic(item, "Bounds", bounds);
        SetPublic(item, "CapturedImageOverride", SyntheticBitmap((int)bounds.Width, (int)bounds.Height, false));
        ((IList)Get(overlay, "_selections")).Add(item);
        Invoke(overlay, "UpdateSelection", item);
        return item;
    }
    private static void SelectDrawingObject(CaptureOverlayWindow overlay, object item, Point point)
    {
        var markup = Property<InkCanvas>(item, "Markup");
        Require((bool)Invoke(overlay, "BeginDrawingObjectSelection", item, point, markup)!, "A visible annotation could not be selected");
        Invoke(overlay, "CommitSelectedDrawingMove");
        markup.ReleaseMouseCapture();
    }
    private static void FocusControl(CaptureOverlayWindow overlay, string name)
    {
        var control = (FrameworkElement)overlay.FindName(name);
        control.Focus();
        overlay.UpdateLayout();
    }
    private static void Clear(CaptureOverlayWindow overlay) => Invoke(overlay, "DrawClear", overlay, new RoutedEventArgs());
    private static void Undo(CaptureOverlayWindow overlay) => Invoke(overlay, "DrawUndo", overlay, new RoutedEventArgs());
    private static void Redo(CaptureOverlayWindow overlay) => Invoke(overlay, "DrawRedo", overlay, new RoutedEventArgs());
    private static int[] Numbers(IList elements) => elements.Cast<object>().Select(element => Property<int>(element, "Number")).ToArray();
    private static object ById(IList elements, Guid id) => elements.Cast<object>().Single(element => Property<Guid>(element, "Id") == id);
    private static Point Center(object element) => new(Property<double>(element, "X") + Property<double>(element, "Diameter") / 2,
        Property<double>(element, "Y") + Property<double>(element, "Diameter") / 2);
    private static Point PointOf(StylusPoint point) => new(point.X, point.Y);
    private static BitmapSource Render(CaptureOverlayWindow overlay, object item)
    {
        overlay.UpdateLayout();
        return (BitmapSource)Invoke(overlay, "RenderSelectionImage", item, true, true, true)!;
    }
    private static BitmapSource SyntheticBitmap(int width, int height, bool patterned)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            pixels[offset] = patterned ? (byte)((x * 11 + y * 7) % 256) : (byte)255;
            pixels[offset + 1] = patterned ? (byte)((x * 3 + y * 17) % 256) : (byte)255;
            pixels[offset + 2] = patterned ? (byte)((x * 19 + y * 5) % 256) : (byte)255;
            pixels[offset + 3] = 255;
        }
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        image.Freeze();
        return image;
    }
    private static byte[] Pixels(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        return pixels;
    }
    private static bool SamePixels(BitmapSource a, BitmapSource b)
        => a.PixelWidth == b.PixelWidth && a.PixelHeight == b.PixelHeight && Pixels(a).SequenceEqual(Pixels(b));
    private static int CountColor(BitmapSource image, Func<byte, byte, byte, bool> predicate)
    {
        var pixels = Pixels(image); var count = 0;
        for (var index = 0; index < pixels.Length; index += 4)
            if (predicate(pixels[index + 2], pixels[index + 1], pixels[index])) count++;
        return count;
    }
    private static int DistinctColors(BitmapSource image, Int32Rect region)
    {
        var pixels = Pixels(image); var colors = new HashSet<int>();
        for (var y = region.Y; y < region.Y + region.Height; y++) for (var x = region.X; x < region.X + region.Width; x++)
        {
            var index = (y * image.PixelWidth + x) * 4;
            colors.Add(pixels[index] | pixels[index + 1] << 8 | pixels[index + 2] << 16);
        }
        return colors.Count;
    }
    private static bool Red(byte r, byte g, byte b) => r > 220 && g < 50 && b < 50;
    private static bool Blue(byte r, byte g, byte b) => b > 220 && r < 50 && g < 50;
    private static async Task Until(Func<bool> condition, string error)
    {
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(25);
        }
        throw new InvalidOperationException(error);
    }
    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name)!.GetValue(target)!;
    private static object Get(object target, string name) => target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name, Private)!.SetValue(target, value);
    private static void SetPublic(object target, string name, object? value) => target.GetType().GetField(name)!.SetValue(target, value);
    private static object? Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private)!.Invoke(target, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
