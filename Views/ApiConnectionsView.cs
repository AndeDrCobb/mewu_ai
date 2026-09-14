// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;

namespace mewu_ai_Assistant.Views;

/// <summary>Shows saved API connections separately from the templates used to create them.</summary>
internal sealed class ApiConnectionsView : StackPanel
{
    private readonly FrameworkElement _editor;
    private readonly Func<AiProviderSettings, bool> _select;
    private readonly Action<ProviderPreset> _add;
    private readonly Action<AiProviderSettings> _setDefault;
    private readonly Action<AiProviderSettings> _remove;
    private readonly Func<AiProviderSettings, string, bool> _rename;
    private IReadOnlyList<AiProviderSettings> _providers = [];
    private string? _defaultId;
    private AiProviderSettings? _selected;
    private AiProviderSettings? _renaming;
    private bool _adding;
    private Border? _editorHost;
    private ContextMenu? _openMenu;

    internal ApiConnectionsView(FrameworkElement editor, Func<AiProviderSettings, bool> select,
        Action<ProviderPreset> add, Action<AiProviderSettings> setDefault,
        Action<AiProviderSettings> remove, Func<AiProviderSettings, string, bool> rename)
    {
        _editor = editor;
        _select = select;
        _add = add;
        _setDefault = setDefault;
        _remove = remove;
        _rename = rename;
        TextElement.SetFontWeight(this, FontWeights.Normal);
        TextElement.SetFontSize(this, 13);
        Margin = new Thickness(6, 4, 6, 8);
    }

    internal void Refresh(IReadOnlyList<AiProviderSettings> providers, string? defaultId,
        AiProviderSettings? selected)
    {
        _providers = providers.ToArray();
        _defaultId = defaultId;
        _selected = selected is not null && _providers.Contains(selected) ? selected : null;
        _renaming = null;
        _adding = false;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_openMenu is not null) _openMenu.IsOpen = false;
        _openMenu = null;
        // The editor owns its input drafts; move the same instance instead of reconstructing it.
        if (_editorHost is not null) _editorHost.Child = null;
        _editorHost = null;
        Children.Clear();

        var heading = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heading.Children.Add(Label(T("我的 API 连接", "My API connections"), 14, true));
        var add = Button(T("＋ 添加连接", "+ Add connection"));
        add.Margin = new Thickness(12, 0, 0, 0);
        add.Click += (_, _) =>
        {
            _adding = !_adding;
            _renaming = null;
            Rebuild();
        };
        Grid.SetColumn(add, 1);
        heading.Children.Add(add);
        Children.Add(heading);

        var description = Label(T("保存你自己的服务地址、密钥和模型。", "Save your own service address, API key and model."), 12);
        description.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
        description.TextWrapping = TextWrapping.Wrap;
        description.Margin = new Thickness(0, 0, 0, 12);
        Children.Add(description);

        if (_adding) Children.Add(BuildTemplates());
        foreach (var provider in _providers) Children.Add(BuildConnection(provider));
        if (_providers.Count == 0)
        {
            var empty = Label(T("还没有连接，点击“添加连接”开始设置。", "No connections yet. Choose Add connection to get started."), 12);
            empty.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
            empty.TextWrapping = TextWrapping.Wrap;
            empty.Margin = new Thickness(0, 8, 0, 12);
            Children.Add(empty);
        }
    }

    private FrameworkElement BuildTemplates()
    {
        var content = new StackPanel();
        content.Children.Add(Label(T("选择服务商", "Choose a service"), 13, true));
        var search = new TextBox { Height = 34,
            ToolTip = T("搜索服务商、模型品牌或地址", "Search services, model families or endpoints") };
        AutomationProperties.SetName(search, T("搜索服务商", "Search services"));
        var searchRow = new Grid { Margin = new Thickness(0, 10, 0, 8) };
        var placeholder = Label(T("搜索服务商…", "Search services…"), 12);
        placeholder.IsHitTestVisible = false;
        placeholder.Margin = new Thickness(12, 0, 0, 0);
        placeholder.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
        searchRow.Children.Add(search);
        searchRow.Children.Add(placeholder);
        content.Children.Add(searchRow);
        var choices = new StackPanel();
        var scroll = new ScrollViewer { Content = choices, MaxHeight = 210,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        content.Children.Add(scroll);
        void PopulateChoices()
        {
            choices.Children.Clear();
            foreach (var group in Enum.GetValues<ProviderPresetGroup>())
            {
                var presets = ProviderPresetPolicy.All.Where(p => p.Group == group && p.Matches(search.Text)).ToArray();
                if (presets.Length == 0) continue;
                var title = Label(group switch {
                    ProviderPresetGroup.China => T("国内服务", "China services"),
                    ProviderPresetGroup.Global => T("国际服务", "Global services"),
                    _ => T("其他服务", "Other services") }, 11);
                title.Margin = new Thickness(0, 2, 0, 6);
                title.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
                choices.Children.Add(title);
                var row = new WrapPanel();
                foreach (var preset in presets)
                {
                    var choice = Button(PresetName(preset));
                    choice.Margin = new Thickness(0, 0, 8, 8);
                    choice.ToolTip = string.IsNullOrEmpty(preset.BaseUrl) ? T("填写你自己的兼容 API 地址", "Enter your compatible API endpoint") : preset.BaseUrl;
                    choice.Tag = preset;
                    choice.Click += (_, _) => _add(preset);
                    row.Children.Add(choice);
                }
                choices.Children.Add(row);
            }
            if (choices.Children.Count == 0)
            {
                var empty = Label(T("没有匹配的服务商，可使用自定义兼容服务。", "No matching service. Try a custom compatible service."), 11);
                empty.TextWrapping = TextWrapping.Wrap;
                choices.Children.Add(empty);
            }
            scroll.ScrollToTop();
        }
        search.TextChanged += (_, _) => { placeholder.Visibility = string.IsNullOrEmpty(search.Text) ? Visibility.Visible : Visibility.Collapsed; PopulateChoices(); };
        PopulateChoices();
        var cancel = Button(T("取消", "Cancel"));
        cancel.Margin = new Thickness(0, 6, 0, 0);
        cancel.HorizontalAlignment = HorizontalAlignment.Left;
        cancel.Click += (_, _) => { _adding = false; Rebuild(); };
        content.Children.Add(cancel);
        var border = Frame(content);
        border.Padding = new Thickness(12);
        return border;
    }

    private FrameworkElement BuildConnection(AiProviderSettings provider)
    {
        var expanded = ReferenceEquals(provider, _selected);
        var content = new StackPanel();
        var header = new Grid { Margin = new Thickness(4) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var toggle = Button(string.Empty);
        toggle.Padding = new Thickness(8, 6, 8, 6);
        toggle.BorderThickness = new Thickness(0);
        toggle.Background = Brushes.Transparent;
        toggle.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        toggle.Content = BuildSummary(provider, expanded);
        AutomationProperties.SetName(toggle, T($"{provider.Name}，{(expanded ? "收起" : "编辑连接")}",
            $"{provider.Name}, {(expanded ? "collapse" : "edit connection")}"));
        toggle.Click += (_, _) =>
        {
            if (ReferenceEquals(_selected, provider))
            {
                _selected = null;
                _renaming = null;
                Rebuild();
                return;
            }
            // The owner validates and stores the old draft, then calls Refresh on success.
            _select(provider);
        };
        header.Children.Add(toggle);

        var more = Button(string.Empty);
        more.Width = 38;
        more.Height = 38;
        more.Padding = new Thickness(0);
        more.BorderThickness = new Thickness(0);
        more.Background = Brushes.Transparent;
        more.HorizontalContentAlignment = HorizontalAlignment.Center;
        more.VerticalContentAlignment = VerticalAlignment.Center;
        var dots = new GeometryGroup();
        foreach (var x in new[] { 3d, 9d, 15d })
            dots.Children.Add(new EllipseGeometry(new Point(x, 9), 1, 1));
        dots.Freeze();
        var moreIcon = new System.Windows.Shapes.Path
        {
            Width = 18, Height = 18, Data = dots, Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        moreIcon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "PrimaryText");
        more.Content = moreIcon;
        more.Margin = new Thickness(0, 0, 4, 0);
        more.ToolTip = T("连接操作", "Connection actions");
        AutomationProperties.SetName(more, T($"{provider.Name}，连接操作", $"{provider.Name}, connection actions"));
        more.Click += (_, _) => OpenMenu(provider, more);
        Grid.SetColumn(more, 1);
        header.Children.Add(more);
        content.Children.Add(header);

        if (ReferenceEquals(provider, _renaming)) content.Children.Add(BuildRename(provider));
        if (expanded)
        {
            _editorHost = new Border { Padding = new Thickness(12, 8, 12, 14), Child = _editor };
            content.Children.Add(_editorHost);
        }
        return Frame(content);
    }

    private FrameworkElement BuildSummary(AiProviderSettings provider, bool expanded)
    {
        var summary = new Grid();
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        summary.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        summary.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var chevron = new System.Windows.Shapes.Path
        {
            Width = 10, Height = 10, Stretch = Stretch.Uniform,
            Data = Geometry.Parse(expanded ? "M 1,3 L 5,7 L 9,3" : "M 3,1 L 7,5 L 3,9"),
            StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center
        };
        chevron.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "SecondaryText");
        Grid.SetRowSpan(chevron, 2);
        summary.Children.Add(chevron);

        var name = Label(provider.Name, 13, true);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.ToolTip = provider.Name;
        LocalizationService.SetExcludeFromLocalization(name, true);
        Grid.SetColumn(name, 1);
        summary.Children.Add(name);
        if (string.Equals(provider.Id, _defaultId, StringComparison.Ordinal))
        {
            var badgeText = Label(T("默认", "Default"), 10);
            badgeText.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
            var badge = new Border
            {
                Child = badgeText, CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(8, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(240, 242, 255)), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(badge, 2);
            Grid.SetRowSpan(badge, 2);
            summary.Children.Add(badge);
        }

        var model = string.IsNullOrWhiteSpace(provider.Model) ? T("未选择模型", "No model selected") : provider.Model;
        var details = Label($"{PresetName(ProviderPresetPolicy.Detect(provider))} · {model}", 11);
        details.Margin = new Thickness(0, 3, 0, 0);
        details.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
        details.TextTrimming = TextTrimming.CharacterEllipsis;
        details.ToolTip = details.Text;
        LocalizationService.SetExcludeFromLocalization(details, true);
        Grid.SetColumn(details, 1);
        Grid.SetRow(details, 1);
        summary.Children.Add(details);
        return summary;
    }

    private FrameworkElement BuildRename(AiProviderSettings provider)
    {
        var row = new Grid { Margin = new Thickness(12, 4, 12, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var input = new TextBox { Text = provider.Name, MaxLength = 80, VerticalContentAlignment = VerticalAlignment.Center };
        AiSettingsForm.PrepareEditor(input);
        LocalizationService.SetExcludeFromLocalization(input, true);
        AutomationProperties.SetName(input, T("连接名称", "Connection name"));
        row.Children.Add(input);
        void Confirm()
        {
            if (_rename(provider, input.Text))
            {
                _renaming = null;
                Rebuild();
            }
            else
            {
                input.ToolTip = T("请输入 1–80 个字符的名称。", "Enter a name with 1–80 characters.");
                input.BorderBrush = Brushes.Firebrick;
            }
        }
        void Cancel() { _renaming = null; Rebuild(); }
        var confirm = Button(T("确认", "Confirm"));
        confirm.Margin = new Thickness(8, 0, 0, 0);
        confirm.Click += (_, _) => Confirm();
        Grid.SetColumn(confirm, 1);
        row.Children.Add(confirm);
        var cancel = Button(T("取消", "Cancel"));
        cancel.Margin = new Thickness(8, 0, 0, 0);
        cancel.Click += (_, _) => Cancel();
        Grid.SetColumn(cancel, 2);
        row.Children.Add(cancel);
        input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; Confirm(); }
            else if (e.Key == Key.Escape) { e.Handled = true; Cancel(); }
        };
        input.Loaded += (_, _) => input.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (!input.IsLoaded || !ReferenceEquals(_renaming, provider)) return;
            input.Focus();
            input.SelectAll();
        }));
        return row;
    }

    private void OpenMenu(AiProviderSettings provider, Button target)
    {
        if (_openMenu is not null) _openMenu.IsOpen = false;
        var menu = new ContextMenu
        {
            PlacementTarget = target, Placement = PlacementMode.Bottom,
            HorizontalOffset = -28, VerticalOffset = -24
        };
        menu.SetResourceReference(StyleProperty, typeof(ContextMenu));
        void AddItem(string label, Action action, bool enabled = true)
        {
            var item = new MenuItem { Header = label, IsEnabled = enabled };
            item.SetResourceReference(StyleProperty, typeof(MenuItem));
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        AddItem(T("重命名", "Rename"), () => { _renaming = provider; _adding = false; Rebuild(); });
        AddItem(T("设为默认", "Set as default"), () => _setDefault(provider),
            !string.Equals(provider.Id, _defaultId, StringComparison.Ordinal));
        var separator = new Separator();
        separator.SetResourceReference(StyleProperty, typeof(Separator));
        menu.Items.Add(separator);
        AddItem(T("删除连接", "Delete connection"), () => _remove(provider), _providers.Count > 1);
        _openMenu = menu;
        menu.IsOpen = true;
    }

    private static Border Frame(UIElement content)
    {
        var frame = new Border
        {
            Child = content, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 8), Background = Brushes.White, SnapsToDevicePixels = true
        };
        frame.SetResourceReference(Border.BorderBrushProperty, "Hairline");
        return frame;
    }

    private static Button Button(string text)
    {
        var button = new Button
        {
            Content = text, MinWidth = 0, MinHeight = 38, FontSize = 12,
            FontWeight = FontWeights.Normal, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.SetResourceReference(StyleProperty, "SecondaryButton");
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static TextBlock Label(string text, double fontSize, bool strong = false)
    {
        var label = new TextBlock
        {
            Text = text, FontSize = fontSize, FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
        return label;
    }

    private static string PresetName(ProviderPreset preset) => preset.LocalizedName;

    private static string T(string chinese, string english) => LocalizationService.T(chinese, english);
}
