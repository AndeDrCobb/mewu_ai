// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using mewu_ai_Assistant.Views;
using Application = System.Windows.Application;
using Border = System.Windows.Controls.Border;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

/// <summary>Actual settings controls with synthetic drafts; no AppHost, settings, credentials or network.</summary>
internal static class ProviderTemplatesReplay
{
    internal static void Run(Application app, bool english)
    {
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var language = english ? "en" : "zh";
        var checks = new List<string>();
        var chosen = new List<string>();
        var selected = 0;
        var changedDefault = 0;
        var removed = 0;
        var renamed = 0;
        var editor = new TextBox { Text = "synthetic-unsaved-model", Height = 34 };
        editor.Select(3, 7);
        var providers = new[]
        {
            new AiProviderSettings
            {
                Id = "qa-synthetic-legacy", Name = "MiniMax QA", Type = "MiniMax",
                BaseUrl = "https://api.minimaxi.com/v1", Model = "MiniMax-M3",
                CredentialId = "qa-reference-only-not-a-real-credential",
                CustomHeaders = new() { ["X-Synthetic-Header"] = "local-fixture" },
                SensitiveHeaderCredentialIds = new() { ["X-Synthetic-Secret"] = "qa-reference-only" }
            },
            new AiProviderSettings
            {
                Id = "qa-synthetic-custom", Name = "Synthetic custom connection with a deliberately long English name",
                Type = "OpenAICompatible", BaseUrl = "https://qa.example.invalid/v1",
                Model = "synthetic-model-" + new string('x', 150)
            }
        };
        var original = JsonSerializer.Serialize(providers);
        var view = new ApiConnectionsView(editor,
            _ => { selected++; return false; }, preset => chosen.Add(preset.Id),
            _ => changedDefault++, _ => removed++, (_, _) => { renamed++; return false; });
        view.Refresh(providers, providers[0].Id, providers[0]);
        var content = new Grid { Background = Brushes.White };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition());
        var banner = new TextBlock
        {
            Text = english ? "Synthetic UI check · no saved settings or network" : "合成界面验收 · 不读取设置或访问网络",
            FontSize = 12, Foreground = Brushes.SlateGray, Margin = new Thickness(18, 12, 18, 8),
            TextWrapping = TextWrapping.Wrap
        };
        content.Children.Add(banner);
        var outer = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(12, 4, 12, 12), Child = view },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(outer, 1);
        content.Children.Add(outer);
        var window = new Window
        {
            Title = english ? "API templates · synthetic QA" : "API 服务模板 · 合成验收",
            Width = 700, Height = 610, Content = content, Background = Brushes.White
        };
        window.Show();
        app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
        {
            string? failure = null;
            try
            {
                await Settle(window);
                Require(ProviderPresetPolicy.Detect(providers[0]).Id == "MiniMax", "Legacy MiniMax endpoint was not recognized");
                Require(ProviderPresetPolicy.Detect(providers[1]).Id == "Custom", "Custom endpoint was mistaken for an official service");
                Click(FindButton(view, english ? "+ Add connection" : "＋ 添加连接"));
                await Settle(window);
                var search = Search(view, english);
                var scroll = TemplateScroll(view);
                Require(TemplateButtons(view).Length == 21, "Not all 21 service templates are available");
                Require(new[] { "China services", "Global services", "Other services" }
                    .Select((text, index) => english ? text : new[] { "国内服务", "国际服务", "其他服务" }[index])
                    .All(title => Descendants(view).OfType<TextBlock>().Any(block => block.Text == title)),
                    "Service groups are missing or mixed between languages");
                Require(scroll.MaxHeight == 210 && scroll.ActualHeight <= 210.5 && scroll.ScrollableHeight > 0,
                    "The template list is not bounded to 210 DIP with vertical scrolling");
                VerifyTemplateFit(view, scroll);
                checks.Add("all-21-templates-grouped-and-bounded-with-readable-labels");
                Save(content, $"provider-templates-{language}.png");

                scroll.ScrollToBottom();
                await Settle(window);
                Require(scroll.VerticalOffset > 0 && scroll.VerticalOffset >= scroll.ScrollableHeight - .5,
                    "Global and custom templates cannot be reached by scrolling");
                Save(content, $"provider-templates-{language}-global.png");
                checks.Add("international-and-custom-services-remain-reachable");

                foreach (var (query, ids) in new[]
                {
                    ("qWeN", new[] { "DashScope", "DashScopeGlobal" }),
                    ("  anthropic  ", new[] { "Anthropic" }),
                    ("api.mistral.ai", new[] { "Mistral" })
                })
                {
                    search.Text = query;
                    await Settle(window);
                    Require(TemplateButtons(view).Select(button => ((ProviderPreset)button.Tag).Id)
                        .OrderBy(id => id).SequenceEqual(ids.OrderBy(id => id)), "Search did not match the expected service aliases or endpoints");
                    Require(scroll.VerticalOffset == 0, "Filtering left the template list scrolled past its results");
                }
                checks.Add("search-matches-case-insensitive-family-alias-name-and-endpoint");

                window.Width = 392;
                search.Text = string.Empty;
                await Settle(window);
                VerifyTemplateFit(view, scroll);
                Require(scroll.ActualHeight <= 210.5 && scroll.ScrollableHeight > 0,
                    "Narrow template list grew beyond its height limit");
                scroll.ScrollToBottom();
                await Settle(window);
                Save(content, $"provider-templates-{language}-narrow.png");
                checks.Add("narrow-window-keeps-long-service-labels-inside-the-viewport");

                search.Text = "qa-no-such-service";
                await Settle(window);
                Require(TemplateButtons(view).Length == 0, "Unmatched query still shows templates");
                var empty = Descendants(scroll).OfType<TextBlock>().Single(block => block.Text.StartsWith(
                    english ? "No matching service." : "没有匹配的服务商", StringComparison.Ordinal));
                Require(empty.TextWrapping == TextWrapping.Wrap && empty.ActualWidth <= scroll.ViewportWidth + .5,
                    "The no-results message is clipped at narrow width");
                Save(content, $"provider-templates-{language}-empty.png");
                checks.Add("empty-search-results-wrap-in-the-narrow-window");

                Click(FindButton(view, english ? "Cancel" : "取消"));
                await Settle(window);
                Require(!Descendants(view).OfType<TextBox>().Any(box => AutomationProperties.GetName(box) ==
                    (english ? "Search services" : "搜索服务商")), "Cancel left the template picker open");
                Require(Descendants(view).Contains(editor) && editor.Text == "synthetic-unsaved-model" &&
                    editor.SelectionStart == 3 && editor.SelectionLength == 7,
                    "Opening or canceling templates replaced the editor or lost its draft selection");
                Require(chosen.Count == 0 && selected == 0 && changedDefault == 0 && removed == 0 && renamed == 0,
                    "Browsing or canceling templates invoked a connection mutation");
                Require(JsonSerializer.Serialize(providers) == original,
                    "Browsing templates changed an existing endpoint, credential reference or default connection");
                checks.Add("search-and-cancel-preserve-editor-draft-existing-connections-and-credential-references");

                Click(FindButton(view, english ? "+ Add connection" : "＋ 添加连接"));
                Search(view, english).Text = "anthropic";
                await Settle(window);
                Click(TemplateButtons(view).Single());
                Require(chosen.SequenceEqual(new[] { "Anthropic" }) && selected == 0 && changedDefault == 0 && removed == 0 && renamed == 0,
                    "Selecting a template did not exclusively invoke its explicit add callback");
                Require(JsonSerializer.Serialize(providers) == original, "Selecting a template changed an existing connection");
                checks.Add("explicit-template-selection-only-requests-adding-that-template");
            }
            catch (Exception ex) { failure = ex.ToString(); Environment.ExitCode = 1; }
            finally
            {
                window.Close();
                Directory.CreateDirectory(".codex-build");
                File.WriteAllText($".codex-build/provider-templates-{language}-result.json",
                    JsonSerializer.Serialize(new { language, checks, failure }, new JsonSerializerOptions { WriteIndented = true }),
                    new System.Text.UTF8Encoding(false));
                app.Shutdown(Environment.ExitCode);
            }
        }));
    }

    private static async Task Settle(Window window)
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private static TextBox Search(DependencyObject view, bool english) => Descendants(view).OfType<TextBox>()
        .Single(box => AutomationProperties.GetName(box) == (english ? "Search services" : "搜索服务商"));

    private static ScrollViewer TemplateScroll(DependencyObject view) => Descendants(view).OfType<ScrollViewer>()
        .Single(scroll => scroll.Content is StackPanel && scroll.MaxHeight == 210);

    private static Button[] TemplateButtons(DependencyObject view) => Descendants(view).OfType<Button>()
        .Where(button => button.Tag is ProviderPreset).ToArray();

    private static Button FindButton(DependencyObject view, string content) => Descendants(view).OfType<Button>()
        .Single(button => Equals(button.Content, content));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void VerifyTemplateFit(DependencyObject view, ScrollViewer scroll)
    {
        foreach (var button in TemplateButtons(view))
        {
            var origin = button.TransformToAncestor(scroll).Transform(new Point(0, 0));
            Require(origin.X >= -.5 && origin.X + button.ActualWidth <= scroll.ViewportWidth + .5,
                $"Service label extends beyond the viewport: {((ProviderPreset)button.Tag).Id}");
            Require(button.ActualHeight >= 38 && button.ActualWidth > 30, "Service button has no usable target");
            foreach (var label in Descendants(button).OfType<TextBlock>())
                Require(label.DesiredSize.Width <= label.ActualWidth + .5 && label.DesiredSize.Height <= label.ActualHeight + .5,
                    $"Service button clips its text: {((ProviderPreset)button.Tag).Id}");
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Save(FrameworkElement element, string fileName)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
            drawing.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.Fill }, null,
                new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(".codex-build");
        using var stream = File.Create(Path.Combine(".codex-build", fileName));
        encoder.Save(stream);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
