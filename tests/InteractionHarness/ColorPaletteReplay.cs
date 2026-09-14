// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using mewu_ai_Assistant.Views;
using Application = System.Windows.Application;
using Border = System.Windows.Controls.Border;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

/// <summary>Opt-in palette replay with a synthetic owner; no AppHost or saved settings.</summary>
internal static class ColorPaletteReplay
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    internal static void Run(Application app)
    {
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var owner = new Window
        {
            Title = "颜色选择验收 · 合成数据",
            Width = 520,
            Height = 620,
            Background = Brushes.WhiteSmoke,
            Content = new TextBlock
            {
                Text = "颜色选择验收 · 合成数据\n不读取配置或剪贴板",
                Margin = new Thickness(20),
                Foreground = Brushes.SlateGray
            }
        };
        owner.Show();
        app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
        {
            var checks = new List<string>();
            string? failure = null;
            MewuColorDialog? dialog = null;
            try
            {
                var initial = Color.FromRgb(49, 140, 255);
                dialog = (MewuColorDialog)typeof(MewuColorDialog)
                    .GetConstructor(Private, null, [typeof(Color)], null)!.Invoke([initial]);
                dialog.Owner = owner;
                dialog.Title = "颜色选择验收 · 合成数据";
                dialog.Show();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                dialog.UpdateLayout();

                var ring = Find<FrameworkElement>(dialog, "HueRing");
                var plane = Find<Grid>(dialog, "SaturationValuePlane");
                Require(!Descendants(dialog).OfType<Slider>().Any(), "The palette still contains RGB sliders");
                Require(ring.ActualWidth > 200 && ring.ActualHeight > 200 &&
                    plane.ActualWidth > 100 && plane.ActualHeight > 100,
                    "The hue ring or two-dimensional color plane is missing");
                Require(ring.Focusable && plane.Focusable, "Palette controls cannot receive keyboard focus");
                checks.Add("full-color-ring-and-two-dimensional-plane-replace-sliders");
                RequireSynchronized(dialog, initial);
                checks.Add("initial-rgb-color-preview-and-hex-are-synchronized");

                Invoke(dialog, "SetSaturationValue", 1d, 1d);
                foreach (var (hue, expected) in new[]
                {
                    (0d, Colors.Red), (120d, Colors.Lime), (240d, Colors.Blue)
                })
                {
                    Invoke(dialog, "SetHue", hue);
                    RequireSynchronized(dialog, expected);
                }
                checks.Add("hue-changes-update-selected-color-preview-rgb-and-hex");

                Invoke(dialog, "SetHue", 210d);
                Invoke(dialog, "SetSaturationValue", .5d, .8d);
                RequireSynchronized(dialog, Color.FromRgb(102, 153, 204));
                RequireSelectorPosition(dialog, .5, .8);
                checks.Add("saturation-value-plane-produces-the-selected-shade");

                Invoke(dialog, "SetHue", 0d);
                dialog.UpdateLayout();
                var ringPixels = Render(ring);
                Require(CountColor(ringPixels, (r, g, b) => r > 200 && g < 90 && b < 90) > 20 &&
                    CountColor(ringPixels, (r, g, b) => g > 200 && r < 90 && b < 90) > 20 &&
                    CountColor(ringPixels, (r, g, b) => b > 200 && r < 90 && g < 90) > 20,
                    "The actual hue-ring pixels do not contain the full primary-color range");
                var planePixels = Render(plane);
                var light = Sample(planePixels, .05, .05);
                var saturated = Sample(planePixels, .95, .05);
                var dark = Sample(planePixels, .5, .95);
                Require(light.R > 220 && light.G > 215 && light.B > 215 &&
                    saturated.R > 220 && saturated.G < 35 && saturated.B < 35 &&
                    dark.R < 25 && dark.G < 25 && dark.B < 25,
                    "The color plane is not rendering white-to-hue and bright-to-black gradients");
                checks.Add("rendered-palette-shows-full-hue-range-and-two-dimensional-gradients");

                Invoke(dialog, "SetChannels", (byte)0, (byte)0, (byte)255);
                var previousHuePosition = SelectorCenter(dialog, "HueSelector");
                Find<TextBox>(dialog, "RedValue").Text = "128";
                Find<TextBox>(dialog, "GreenValue").Text = "64";
                Find<TextBox>(dialog, "BlueValue").Text = "64";
                RequireSynchronized(dialog, Color.FromRgb(128, 64, 64));
                RequireSelectorPosition(dialog, .5, 128d / 255);
                Require((SelectorCenter(dialog, "HueSelector") - previousHuePosition).Length > 30,
                    "Editing RGB left the hue selector at the previous color");
                checks.Add("rgb-input-repositions-both-palette-selectors");

                var hex = Find<TextBox>(dialog, "HexValue");
                hex.Text = "#3399CC";
                RequireSynchronized(dialog, Color.FromRgb(51, 153, 204));
                RequireSelectorPosition(dialog, .75, .8);
                hex.Text = "3366cc";
                RequireSynchronized(dialog, Color.FromRgb(51, 102, 204));
                RequireSelectorPosition(dialog, .75, .8);
                checks.Add("hex-with-or-without-prefix-updates-rgb-preview-and-palette");

                var validColor = Selected(dialog);
                hex.Text = "#GG0000";
                Require(Selected(dialog) == validColor && hex.Text == "#GG0000" &&
                    !Find<Button>(dialog, "ConfirmButton").IsEnabled,
                    "Invalid HEX was silently accepted or enabled confirmation with the old color");
                hex.Text = "#3366CC";
                Require(Find<Button>(dialog, "ConfirmButton").IsEnabled, "Valid HEX did not restore confirmation");
                var red = Find<TextBox>(dialog, "RedValue");
                red.Text = "256";
                Require(Selected(dialog) == validColor && red.Text == "256" &&
                    !Find<Button>(dialog, "ConfirmButton").IsEnabled,
                    "Out-of-range RGB was silently accepted or enabled confirmation with the old color");
                red.Text = "51";
                RequireSynchronized(dialog, validColor);
                checks.Add("invalid-rgb-and-hex-retain-the-draft-and-disable-confirmation");

                Invoke(dialog, "SetChannels", (byte)255, (byte)0, (byte)0);
                RaiseKey(dialog, ring, Key.Right);
                var hueRight = Selected(dialog);
                Require(hueRight.R == 255 && hueRight.G > 0 && hueRight.B == 0,
                    "Right arrow on the hue ring did not adjust its hue");
                RaiseKey(dialog, ring, Key.Left);
                RequireSynchronized(dialog, Colors.Red);
                Invoke(dialog, "SetSaturationValue", .4d, .6d);
                var beforePlaneKey = SelectorCenter(dialog, "SaturationValueSelector");
                RaiseKey(dialog, plane, Key.Right);
                var afterRight = SelectorCenter(dialog, "SaturationValueSelector");
                Require(afterRight.X > beforePlaneKey.X && Math.Abs(afterRight.Y - beforePlaneKey.Y) < .1,
                    "Right arrow on the color plane did not increase saturation independently");
                RaiseKey(dialog, plane, Key.Up);
                var afterUp = SelectorCenter(dialog, "SaturationValueSelector");
                Require(afterUp.Y < afterRight.Y && Math.Abs(afterUp.X - afterRight.X) < .1,
                    "Up arrow on the color plane did not increase brightness independently");
                RequireSynchronized(dialog, Selected(dialog));
                checks.Add("keyboard-adjusts-hue-and-plane-axes-without-sliders");

                foreach (var target in new FrameworkElement[] { ring, plane })
                {
                    Require(target.CaptureMouse(), "A palette control could not own its drag capture");
                    target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonUpEvent, Source = target });
                    Require(!target.IsMouseCaptured, "Releasing a palette drag left the mouse captured");
                }
                checks.Add("releasing-ring-and-plane-drags-releases-mouse-capture");

                Invoke(dialog, "SetChannels", (byte)49, (byte)140, (byte)255);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                dialog.UpdateLayout();
                Save(dialog, "color-palette.png");
                Require(plane.CaptureMouse(), "The close-during-drag fixture could not capture the mouse");
                dialog.Close();
                Require(!plane.IsMouseCaptured, "Closing the palette left the mouse captured");
                checks.Add("closing-the-palette-releases-an-active-drag");
                dialog = null;
                var acceptedColor = Color.FromRgb(136, 221, 187);
                VerifyTryChoose(app, owner, initial, acceptedColor, accept: false);
                checks.Add("cancel-keeps-original-color-and-does-not-close-owner");
                VerifyTryChoose(app, owner, initial, acceptedColor, accept: true);
                checks.Add("confirm-returns-current-color-and-does-not-close-owner");
            }
            catch (Exception ex) { failure = ex.ToString(); Environment.ExitCode = 1; }
            finally
            {
                try { dialog?.Close(); } catch { }
                foreach (var remaining in app.Windows.OfType<MewuColorDialog>().ToArray()) remaining.Close();
                owner.Close();
                Directory.CreateDirectory(".codex-build");
                File.WriteAllText(".codex-build/color-palette-result.json", JsonSerializer.Serialize(new { checks, failure }),
                    new System.Text.UTF8Encoding(false));
                app.Shutdown(Environment.ExitCode);
            }
        }));
    }

    private static void VerifyTryChoose(Application app, Window owner, Color initial, Color chosen, bool accept)
    {
        Exception? callbackFailure = null;
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = app.Windows.OfType<MewuColorDialog>().Single();
            try
            {
                Require(dialog.Owner == owner, "The color dialog lost its synthetic owner");
                Invoke(dialog, "SetChannels", chosen.R, chosen.G, chosen.B);
                RequireSynchronized(dialog, chosen);
                Find<Button>(dialog, accept ? "ConfirmButton" : "CancelButton")
                    .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }
            catch (Exception ex) { callbackFailure = ex; dialog.Close(); }
        }));
        object?[] arguments = [owner, initial, null, "颜色选择验收 · 合成数据"];
        var accepted = (bool)typeof(MewuColorDialog).GetMethod("TryChoose", StaticPrivate)!.Invoke(null, arguments)!;
        if (callbackFailure is not null) throw new InvalidOperationException("Modal palette replay failed", callbackFailure);
        Require(accepted == accept && arguments[2] is Color selected && selected == (accept ? chosen : initial),
            "TryChoose did not preserve the confirm/cancel result contract");
        Require(owner.IsVisible && !app.Windows.OfType<MewuColorDialog>().Any(),
            "Closing the color dialog closed its owner or left another palette open");
    }

    private static void RequireSynchronized(MewuColorDialog dialog, Color expected)
    {
        dialog.UpdateLayout();
        Require(Selected(dialog) == expected, $"Selected color differs from expected #{expected.R:X2}{expected.G:X2}{expected.B:X2}");
        Require(Find<TextBox>(dialog, "RedValue").Text == expected.R.ToString(CultureInfo.InvariantCulture) &&
            Find<TextBox>(dialog, "GreenValue").Text == expected.G.ToString(CultureInfo.InvariantCulture) &&
            Find<TextBox>(dialog, "BlueValue").Text == expected.B.ToString(CultureInfo.InvariantCulture),
            "RGB fields do not match the selected color");
        Require(string.Equals(Find<TextBox>(dialog, "HexValue").Text.TrimStart('#'),
            $"{expected.R:X2}{expected.G:X2}{expected.B:X2}", StringComparison.OrdinalIgnoreCase),
            "HEX input does not match the selected color");
        var preview = Find<Border>(dialog, "ColorPreview");
        Require(preview.Background is SolidColorBrush brush && brush.Color == expected,
            "The preview background does not match the selected color");
        Require(CountColor(Render(preview), (r, g, b) => r == expected.R && g == expected.G && b == expected.B) > 40,
            "The actual preview pixels do not show the selected color");
        Require(Find<Button>(dialog, "ConfirmButton").IsEnabled, "A valid selected color cannot be confirmed");
    }

    private static void RequireSelectorPosition(MewuColorDialog dialog, double saturation, double value)
    {
        dialog.UpdateLayout();
        var plane = Find<FrameworkElement>(dialog, "SaturationValuePlane");
        var origin = plane.TransformToAncestor(dialog).Transform(new Point(0, 0));
        var selector = SelectorCenter(dialog, "SaturationValueSelector") - origin;
        Require(Math.Abs(selector.X - saturation * plane.ActualWidth) < 2 &&
            Math.Abs(selector.Y - (1 - value) * plane.ActualHeight) < 2,
            "RGB/HEX values did not update the actual saturation-value selector position");
    }

    private static Point SelectorCenter(MewuColorDialog dialog, string name)
    {
        dialog.UpdateLayout();
        var selector = Find<FrameworkElement>(dialog, name);
        return selector.TransformToAncestor(dialog).Transform(new Point(selector.ActualWidth / 2, selector.ActualHeight / 2));
    }

    private static void RaiseKey(MewuColorDialog dialog, FrameworkElement target, Key key)
    {
        target.Focus();
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dialog)!, Environment.TickCount, key)
        { RoutedEvent = Keyboard.KeyDownEvent, Source = target };
        target.RaiseEvent(args);
        Require(args.Handled, "The focused palette did not handle its adjustment key");
        dialog.UpdateLayout();
    }

    private static Color Selected(MewuColorDialog dialog)
        => (Color)typeof(MewuColorDialog).GetProperty("SelectedColor", Private)!.GetValue(dialog)!;
    private static T Find<T>(MewuColorDialog dialog, string name) where T : FrameworkElement
        => (T)(dialog.FindName(name) ?? throw new InvalidOperationException($"Palette control {name} is missing"));
    private static object? Invoke(object target, string name, params object[] arguments)
        => target.GetType().GetMethod(name, Private)!.Invoke(target, arguments);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static BitmapSource Render(FrameworkElement element)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
            drawing.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.Fill }, null,
                new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
    private static Color Sample(BitmapSource bitmap, double x, double y)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect((int)(bitmap.PixelWidth * x), (int)(bitmap.PixelHeight * y), 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }
    private static int CountColor(BitmapSource bitmap, Func<byte, byte, byte, bool> match)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        var count = 0;
        for (var index = 0; index < pixels.Length; index += 4)
            if (pixels[index + 3] == 255 && match(pixels[index + 2], pixels[index + 1], pixels[index])) count++;
        return count;
    }
    private static void Save(FrameworkElement element, string name)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Render(element)));
        Directory.CreateDirectory(".codex-build");
        using var stream = File.Create(Path.Combine(".codex-build", name));
        encoder.Save(stream);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
