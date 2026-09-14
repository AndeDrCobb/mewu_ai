// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Xml.Linq;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using mewu_ai_Assistant.Views;
using Xunit;

namespace MewuAI.Tests;

[Collection("WPF theme resources")]
public sealed class ApiConnectionsViewTests
{
    [Fact]
    public void CollapsingAndReopeningRetainsSameEditorAndUnsavedSelection()
    {
        RunSta(() =>
        {
            var provider = Provider("Personal connection");
            var editor = new TextBox { Text = "unsaved-model-draft" };
            editor.Select(3, 5);
            var selectionCalls = 0;
            ApiConnectionsView view = null!;
            view = Create(editor, selected =>
            {
                selectionCalls++;
                view.Refresh([provider], provider.Id, selected);
                return true;
            });
            view.Refresh([provider], provider.Id, provider);
            Layout(view);
            var initialHost = Assert.IsType<Border>(editor.Parent);
            Assert.Same(editor, Assert.Single(Descendants<TextBox>(view)));

            Click(ConnectionButton(view, provider));
            Layout(view);
            Assert.Null(editor.Parent);
            Assert.Null(initialHost.Child);
            Assert.Empty(Descendants<TextBox>(view));
            Assert.Equal(0, selectionCalls);

            Click(ConnectionButton(view, provider));
            Layout(view);
            Assert.Equal(1, selectionCalls);
            Assert.Same(editor, Assert.Single(Descendants<TextBox>(view)));
            Assert.Equal("unsaved-model-draft", editor.Text);
            Assert.Equal(3, editor.SelectionStart);
            Assert.Equal(5, editor.SelectionLength);
        });
    }

    [Fact]
    public void RejectedSelectionKeepsCurrentDraftAndAcceptedSelectionMovesSharedEditor()
    {
        RunSta(() =>
        {
            var first = Provider("First connection");
            var second = Provider("Second connection");
            var editor = new TextBox { Text = "unfinished-first-model" };
            var allowSelection = false;
            var selectionCalls = 0;
            string? storedDraft = null;
            ApiConnectionsView view = null!;
            view = Create(editor, selected =>
            {
                selectionCalls++;
                if (!allowSelection) return false;
                storedDraft = editor.Text;
                editor.Text = selected.Model;
                view.Refresh([first, second], first.Id, selected);
                return true;
            });
            view.Refresh([first, second], first.Id, first);
            Layout(view);
            var firstHost = Assert.IsType<Border>(editor.Parent);

            Click(ConnectionButton(view, second));
            Assert.Equal(1, selectionCalls);
            Assert.Same(firstHost, editor.Parent);
            Assert.Equal("unfinished-first-model", editor.Text);
            Assert.Contains(T("收起", "collapse"), AutomationProperties.GetName(ConnectionButton(view, first)));

            allowSelection = true;
            Click(ConnectionButton(view, second));
            Layout(view);
            Assert.Equal(2, selectionCalls);
            Assert.Equal("unfinished-first-model", storedDraft);
            Assert.Null(firstHost.Child);
            Assert.NotSame(firstHost, editor.Parent);
            Assert.Same(editor, Assert.Single(Descendants<TextBox>(view)));
            Assert.Equal(second.Model, editor.Text);
            Assert.Contains(T("收起", "collapse"), AutomationProperties.GetName(ConnectionButton(view, second)));
            Assert.Contains(T("编辑连接", "edit connection"), AutomationProperties.GetName(ConnectionButton(view, first)));
        });
    }

    [Fact]
    public void MenuProtectsOnlyConnectionAndRejectedRenameRemainsEditable()
    {
        RunSta(() =>
        {
            var provider = Provider("Original name");
            var renameCalls = 0;
            var view = Create(new TextBox(), _ => true, rename: (selected, name) =>
            {
                renameCalls++;
                if (string.IsNullOrWhiteSpace(name)) return false;
                selected.Name = name;
                return true;
            });
            view.Refresh([provider], provider.Id, null);
            Layout(view);
            try
            {
                Click(ActionsButton(view, provider));
                var menu = OpenMenu(view);
                Assert.False(Item(menu, T("设为默认", "Set as default")).IsEnabled);
                Assert.False(Item(menu, T("删除连接", "Delete connection")).IsEnabled);
                Item(menu, T("重命名", "Rename")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var input = Assert.Single(Descendants<TextBox>(view));
                input.Text = " ";
                Click(Descendants<Button>(view).Single(button => Equals(button.Content, T("确认", "Confirm"))));
                Assert.Equal(1, renameCalls);
                Assert.Equal("Original name", provider.Name);
                Assert.Same(input, Assert.Single(Descendants<TextBox>(view)));

                input.Text = "Renamed connection";
                Click(Descendants<Button>(view).Single(button => Equals(button.Content, T("确认", "Confirm"))));
                Assert.Equal(2, renameCalls);
                Assert.Equal("Renamed connection", provider.Name);
                Assert.Empty(Descendants<TextBox>(view));
                Assert.NotNull(ConnectionButton(view, provider));

                var second = Provider("Second connection");
                view.Refresh([provider, second], provider.Id, null);
                Click(ActionsButton(view, second));
                menu = OpenMenu(view);
                Assert.True(Item(menu, T("设为默认", "Set as default")).IsEnabled);
                Assert.True(Item(menu, T("删除连接", "Delete connection")).IsEnabled);
            }
            finally { CloseMenu(view); }
        });
    }

    [Fact]
    public void DefaultBadgeCentersOnHeaderAndExpandedContentHasBottomBreathingRoom()
    {
        RunSta(() =>
        {
            var provider = Provider("Personal connection");
            var lastControl = new Expander { Header = T("高级设置", "Advanced settings") };
            var editor = new StackPanel();
            editor.Children.Add(new TextBox { Text = "model-draft", Margin = new Thickness(0, 0, 0, 12) });
            editor.Children.Add(lastControl);
            var view = Create(editor, _ => true);
            view.Refresh([provider], provider.Id, provider);
            Layout(view, 320);

            var badgeText = Assert.Single(Descendants<TextBlock>(view), label => label.Text == T("默认", "Default"));
            var badge = Assert.IsType<Border>(badgeText.Parent);
            var actions = ActionsButton(view, provider);
            var badgeBounds = badge.TransformToAncestor(view).TransformBounds(new Rect(badge.RenderSize));
            var actionBounds = actions.TransformToAncestor(view).TransformBounds(new Rect(actions.RenderSize));
            Assert.InRange(Math.Abs((badgeBounds.Top + badgeBounds.Height / 2) -
                (actionBounds.Top + actionBounds.Height / 2)), 0, 0.5);

            var glyph = Assert.IsType<System.Windows.Shapes.Path>(actions.Content);
            var paintedBounds = glyph.TransformToAncestor(actions).TransformBounds(glyph.RenderedGeometry.Bounds);
            Assert.InRange(Math.Abs(paintedBounds.Left + paintedBounds.Width / 2 - actions.ActualWidth / 2), 0, 0.5);
            Assert.InRange(Math.Abs(paintedBounds.Top + paintedBounds.Height / 2 - actions.ActualHeight / 2), 0, 0.5);

            var editorHost = Assert.IsType<Border>(editor.Parent);
            var lastBounds = lastControl.TransformToAncestor(editorHost).TransformBounds(new Rect(lastControl.RenderSize));
            Assert.InRange(editorHost.ActualHeight - lastBounds.Bottom, 12, 16);
        });
    }

    [Fact]
    public void LongConnectionNamesLeaveActionsUsableAtNarrowWidth()
    {
        RunSta(() =>
        {
            var provider = Provider(string.Concat(Enumerable.Repeat("Very long school API connection / 学校连接 ", 8)));
            provider.Model = new string('m', 240);
            var created = new List<ProviderPreset>();
            var view = Create(new TextBox(), _ => true, add: preset => created.Add(preset));
            view.Refresh([provider], provider.Id, null);
            Layout(view, 320);
            var actions = ActionsButton(view, provider);
            var add = Descendants<Button>(view).Single(button => Equals(button.Content, T("＋ 添加连接", "+ Add connection")));
            AssertFitsHorizontally(view, actions);
            AssertFitsHorizontally(view, add);
            Assert.True(ConnectionButton(view, provider).ActualWidth >= 80);
            Assert.True(actions.ActualHeight >= 38);
            Assert.True(actions.ActualWidth >= 38);

            Click(add);
            Layout(view, 320);
            var choices = Descendants<Button>(view).Where(button => button.Tag is ProviderPreset).ToArray();
            Assert.Equal(ProviderPresetPolicy.All.Length, choices.Length);
            Assert.True(Assert.Single(Descendants<ScrollViewer>(view),scroll => scroll.MaxHeight == 210).ActualHeight <= 210);
            foreach (var choice in choices)
            {
                AssertFitsHorizontally(view, choice);
                Assert.True(choice.ActualHeight >= 38);
            }
            var search = Descendants<TextBox>(view).Single(box => AutomationProperties.GetName(box) == T("搜索服务商", "Search services"));
            search.Text = "Qwen";
            Layout(view,320);
            Assert.Equal(new[]{"DashScope","DashScopeGlobal"},Descendants<Button>(view).Where(button=>button.Tag is ProviderPreset).Select(button=>((ProviderPreset)button.Tag).Id));
            Assert.Empty(created);
            search.Text = "nonexistent-provider";
            Layout(view,320);
            Assert.DoesNotContain(Descendants<Button>(view),button=>button.Tag is ProviderPreset);
            search.Text = "";
            Layout(view,320);
            Click(Descendants<Button>(view).First(button=>button.Tag is ProviderPreset));
            Assert.Same(ProviderPresetPolicy.All[0], Assert.Single(created));
        });
    }

    private static AiProviderSettings Provider(string name) => new() { Name = name, Model = $"model-{Guid.NewGuid():N}" };

    private static ApiConnectionsView Create(FrameworkElement editor, Func<AiProviderSettings, bool> select,
        Action<ProviderPreset>? add = null, Func<AiProviderSettings, string, bool>? rename = null)
    {
        var view = new ApiConnectionsView(editor, select, add ?? (_ => { }), _ => { }, _ => { }, rename ?? ((_, _) => false));
        var theme = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LightTheme.xaml.xml"));
        // No application or icon resource is needed to exercise the actual control styles.
        theme.Root!.Elements().Where(element => element.Name.LocalName == "Style" && (string?)element.Attribute("TargetType") == "Window").Remove();
        view.Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(theme.ToString());
        return view;
    }

    private static Button ConnectionButton(ApiConnectionsView view, AiProviderSettings provider) =>
        Descendants<Button>(view).Single(button => button.Content is Grid &&
            Descendants<TextBlock>(button).Any(label => label.Text == provider.Name));

    private static Button ActionsButton(ApiConnectionsView view, AiProviderSettings provider) =>
        Descendants<Button>(view).Single(button => AutomationProperties.GetName(button) ==
            T($"{provider.Name}，连接操作", $"{provider.Name}, connection actions"));

    private static ContextMenu OpenMenu(ApiConnectionsView view) => Assert.IsType<ContextMenu>(
        typeof(ApiConnectionsView).GetField("_openMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view));

    private static void CloseMenu(ApiConnectionsView view)
    {
        if (typeof(ApiConnectionsView).GetField("_openMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view) is ContextMenu menu)
            menu.IsOpen = false;
    }

    private static MenuItem Item(ContextMenu menu, string title) => menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, title));
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static string T(string chinese, string english) => LocalizationService.T(chinese, english);

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T typed) yield return typed;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void Layout(FrameworkElement view, double width = 500)
    {
        view.Measure(new Size(width, 1000));
        view.Arrange(new Rect(0, 0, width, Math.Max(1000, view.DesiredSize.Height)));
        view.UpdateLayout();
    }

    private static void AssertFitsHorizontally(FrameworkElement view, FrameworkElement element)
    {
        var bounds = element.TransformToAncestor(view).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        Assert.InRange(bounds.Left, -0.5, view.ActualWidth);
        Assert.InRange(bounds.Right, 0, view.ActualWidth + 0.5);
        Assert.True(element.ActualWidth >= 38);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "API connection view test timed out");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
