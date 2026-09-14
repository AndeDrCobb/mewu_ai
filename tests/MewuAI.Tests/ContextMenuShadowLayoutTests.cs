// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Xml.Linq;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

[Collection("WPF theme resources")]
public sealed class ContextMenuShadowLayoutTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 1.5)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 1.5)]
    [InlineData(true, 2)]
    public void PopupLayoutContainsShadowWithoutApplyingEffectsToContent(bool textSelectionMenu, double scale)
    {
        RunSta(() =>
        {
            var theme = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LightTheme.xaml.xml"));
            theme.Root!.Elements().Where(element => element.Name.LocalName == "Style" &&
                (string?)element.Attribute("TargetType") == "Window").Remove();
            var resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(theme.ToString());
            var menu = new ContextMenu
            {
                Resources = resources,
                Style = (Style)resources[textSelectionMenu ? "TextSelectionContextMenu" : typeof(ContextMenu)]
            };
            menu.Items.Add(new MenuItem { Header = "Rename / 重命名" });
            menu.Items.Add(new MenuItem { Header = "Set as default / 设为默认", IsEnabled = false });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Delete connection / 删除连接" });
            menu.ApplyTemplate();
            menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            menu.Arrange(new Rect(menu.DesiredSize));
            menu.UpdateLayout();

            var root = Assert.IsType<Grid>(VisualTreeHelper.GetChild(menu, 0));
            var surface = Assert.IsType<Border>(menu.Template.FindName("MenuSurface", menu));
            var shadow = Assert.IsType<Border>(menu.Template.FindName("MenuShadow", menu));
            Assert.IsType<DropShadowEffect>(shadow.Effect);
            Assert.Null(surface.Effect);
            Assert.False(shadow.IsHitTestVisible);
            Assert.True(surface.IsHitTestVisible);
            Assert.False(menu.HasDropShadow);
            Assert.Equal(new Thickness(textSelectionMenu ? 6 : 5), surface.Padding);
            Assert.IsType<ItemsPresenter>(surface.Child);
            Assert.True(surface.ActualWidth > 100);
            Assert.True(surface.ActualHeight > 70);

            var bounds = surface.TransformToAncestor(root).TransformBounds(new Rect(surface.RenderSize));
            Assert.InRange(bounds.Left, 27.5, 28.5);
            Assert.InRange(bounds.Top, 27.5, 28.5);
            Assert.InRange(root.ActualWidth - bounds.Right, 27.5, 28.5);
            Assert.InRange(root.ActualHeight - bounds.Bottom, 27.5, 28.5);

            var width = (int)Math.Ceiling(root.ActualWidth * scale);
            var height = (int)Math.Ceiling(root.ActualHeight * scale);
            var bitmap = PinnedVisualSnapshotRenderer.Render(root, width, height);
            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            byte Alpha(int x, int y) => pixels[(y * width + x) * 4 + 3];
            var centerX = (int)((bounds.Left + bounds.Width / 2) * scale);
            var centerY = (int)((bounds.Top + bounds.Height / 2) * scale);
            Assert.True(Enumerable.Range((int)Math.Ceiling(bounds.Right * scale) + 1, (int)(15 * scale))
                .Any(x => Alpha(x, centerY) is > 0 and < 255), "Right shadow must survive outside the menu surface.");
            Assert.True(Enumerable.Range((int)Math.Ceiling(bounds.Bottom * scale) + 1, (int)(15 * scale))
                .Any(y => Alpha(centerX, y) is > 0 and < 255), "Bottom shadow must survive outside the menu surface.");
            Assert.All(Enumerable.Range(0, width), x =>
            {
                Assert.InRange(Alpha(x, 0), 0, 1);
                Assert.InRange(Alpha(x, height - 1), 0, 1);
            });
            Assert.All(Enumerable.Range(0, height), y =>
            {
                Assert.InRange(Alpha(0, y), 0, 1);
                Assert.InRange(Alpha(width - 1, y), 0, 1);
            });
        });
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Context menu shadow layout test timed out");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
