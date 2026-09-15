// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using mewu_ai_Assistant.Views;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;

/// <summary>Real settings controls with synthetic, credential-free drafts. Never starts AppHost or saves settings.</summary>
internal static class SettingsEditingReplay
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Endpoint = "https://example.invalid/v1";
    private const string FirstModel = "synthetic-model-entered-before-url";
    private static readonly string OutputDirectory = Path.Combine(".codex-build", "settings-editing");

    internal static void Run(Application app, AppHost host)
    {
        var fixture = new AppSettings
        {
            DefaultProviderId = "qa-settings-first",
            Providers =
            [
                new() { Id = "qa-settings-first", Name = "Synthetic first connection", Type = "OpenAICompatible", BaseUrl = "", Model = "" },
                new() { Id = "qa-settings-second", Name = "Synthetic second connection", Type = "OpenAICompatible", BaseUrl = Endpoint, Model = "synthetic-second-model" }
            ]
        };
        typeof(AppHost).GetProperty(nameof(AppHost.Settings))!.SetValue(host, fixture);
        var original = JsonSerializer.Serialize(fixture);
        var checks = new List<string>();
        string? failure = null;
        var phase = "create-synthetic-settings";
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var settings = new SettingsWindow(host) { Title = "API settings editing · synthetic QA" };
        using var suppressedUpdate = new CancellationTokenSource();
        typeof(SettingsWindow).GetField("_updateCheck", Private)!.SetValue(settings, suppressedUpdate);
        var model = Field<ComboBox>(settings, "_model");
        var url = Field<TextBox>(settings, "_baseUrl");
        var headers = Field<TextBox>(settings, "_customHeaders");
        var parameters = Field<TextBox>(settings, "_requestParameters");
        var status = Field<TextBlock>(settings, "_modelStatus");
        var advanced = Field<Expander>(settings, "_apiAdvanced");
        Field<TabItem>(settings, "_aiTab").IsSelected = true;
        settings.Loaded += (_, _) => app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await Settle(settings);
                var visibleTitle = Descendants(settings).OfType<TextBlock>().Single(block =>
                    Math.Abs(block.FontSize - 16.5) < .01 && block.FontWeight == FontWeights.SemiBold);
                visibleTitle.Text = LocalizationService.T("设置回放 · 合成数据", "Settings replay · synthetic data");
                Require(!Field<CancellationTokenSource>(settings, "_windowLifetime").IsCancellationRequested,
                    "The replay disabled the settings lifetime instead of exercising real model loading.");
                Require(Field<bool?>(settings, "_captureProtectionAvailable") == true,
                    "The synthetic settings window did not complete native initialization.");
                Require(model.IsLoaded && url.IsLoaded, "The real API editors are not loaded.");
                var editableModel = model.Template.FindName("PART_EditableTextBox", model) as TextBox
                    ?? throw new InvalidOperationException("The real editable model ComboBox has no text editor.");
                phase = "enter-model-before-url";
                editableModel.Focus();
                editableModel.SelectAll();
                editableModel.SelectedText = FirstModel;
                await Settle(settings);
                Require(model.Text == FirstModel && url.Text == "", "Typing the model changed the empty endpoint or lost the model.");
                checks.Add("real-model-editor-accepts-model-before-endpoint");

                phase = "debounced-incomplete-url";
                await ObserveModelStatusAsync(status, () => url.Text = "h", value =>
                    value.Contains("Provider Base URL", StringComparison.Ordinal));
                Require(model.Text == FirstModel, "Debounced invalid URL validation changed the model.");
                checks.Add("actual-url-debounce-reports-incomplete-url-without-unhandled-exception");

                phase = "type-url-character-by-character";
                url.Clear();
                for (var index = 0; index < Endpoint.Length; index++)
                {
                    url.CaretIndex = url.Text.Length;
                    url.SelectedText = Endpoint[index].ToString();
                    await RefreshModels(settings);
                    Require(url.Text == Endpoint[..(index + 1)] && model.Text == FirstModel,
                        $"Typing endpoint character {index + 1} changed the raw draft or model.");
                }
                Require(IsMissingKey(status.Text), "A complete credential-free HTTPS URL did not stop before networking.");
                checks.Add("all-url-prefixes-preserve-model-and-raw-url-draft");

                phase = "delete-and-replace-url";
                foreach (var value in new[] { "", "https:", "https:/", "https://", "https://[", "https://example.invalid:", "https://example.invalid/v1?unfinished", "not a URL", "  " + Endpoint + "  " })
                {
                    url.Text = value;
                    await RefreshModels(settings);
                    Require(url.Text == value && model.Text == FirstModel, "Replacing the endpoint rewrote an unfinished draft.");
                }
                url.Text = Endpoint;
                await RefreshModels(settings);
                checks.Add("url-deletion-invalid-host-query-and-whitespace-drafts-stay-editable");

                phase = "preserve-unfinished-json-on-switch";
                const string unfinishedHeaders = "{\n  \"X-Synthetic-Fixture\":";
                const string unfinishedParameters = "{\n  \"temperature\":";
                headers.Text = unfinishedHeaders;
                parameters.Text = unfinishedParameters;
                url.Text = "https://";
                await RefreshModels(settings);
                var connections = Field<ApiConnectionsView>(settings, "_apiConnections");
                Click(ConnectionButton(connections, fixture.Providers[1].Name));
                await Settle(settings);
                Require(model.Text == "synthetic-second-model" && url.Text == Endpoint,
                    "Switching to the second connection did not load its separate draft.");
                editableModel = model.Template.FindName("PART_EditableTextBox", model) as TextBox
                    ?? throw new InvalidOperationException("The shared model editor was lost after switching connections.");
                editableModel.Text = "synthetic-second-unsaved";
                await Settle(settings);
                Click(ConnectionButton(connections, fixture.Providers[0].Name));
                await Settle(settings);
                Require(model.Text == FirstModel && url.Text == "https://" && headers.Text == unfinishedHeaders && parameters.Text == unfinishedParameters,
                    "Switching connections lost an unfinished model, URL, header or parameter draft.");
                Require(DefaultId(settings) == fixture.DefaultProviderId, "Editing or switching connections changed the default connection.");
                checks.Add("connection-switch-restores-model-url-and-both-unfinished-json-drafts");

                phase = "test-incomplete-json";
                Click(Field<Button>(settings, "_testApiConnection"));
                Require(Field<TextBlock>(settings, "_connectionStatus").Text.Contains("JSON", StringComparison.Ordinal),
                    "Testing incomplete JSON did not display a recoverable validation message.");
                Require(Field<CancellationTokenSource?>(settings, "_connectionTest") is null,
                    "Incomplete JSON unexpectedly started a connection test.");
                parameters.Text = "{}";
                Click(Field<Button>(settings, "_testApiConnection"));
                Require(Field<CancellationTokenSource?>(settings, "_connectionTest") is null && headers.Text == unfinishedHeaders,
                    "Incomplete header JSON started a connection test or was rewritten.");
                checks.Add("connection-test-rejects-each-unfinished-json-draft-before-networking");

                phase = "recover-from-invalid-headers";
                url.Text = Endpoint;
                foreach (var value in new[] { "[]", "{\"X-Synthetic-Fixture\":null}", "{\"Content-Length\":\"1\"}" })
                {
                    headers.Text = value;
                    await RefreshModels(settings);
                    Require(headers.Text == value && model.Text == FirstModel, "Invalid headers destroyed the current draft.");
                }
                headers.Text = "{}";
                await RefreshModels(settings);
                Require(IsMissingKey(status.Text), "Correcting the JSON did not restore ordinary endpoint validation.");
                checks.Add("invalid-header-shapes-and-values-report-errors-and-recover");

                phase = "malformed-key-followed-by-valid-url";
                var apiKey = Field<PasswordBox>(settings, "_apiKey");
                // A CR/LF-bearing synthetic value cannot form an HTTP bearer
                // header. The catalog must reject it before SendAsync.
                url.Text = "https://";
                apiKey.Password = "synthetic\r\ninvalid-key";
                Require(apiKey.Password.Contains('\r') && apiKey.Password.Contains('\n'),
                    "The actual PasswordBox did not retain the malformed key fixture.");
                await RefreshModels(settings);
                url.Text = Endpoint;
                await RefreshModels(settings);
                Require(status.Text.Contains("API", StringComparison.OrdinalIgnoreCase) && !IsMissingKey(status.Text) &&
                    !status.Text.Contains("synthetic", StringComparison.Ordinal) &&
                    model.Text == FirstModel && url.Text == Endpoint,
                    "Malformed API key validation did not safely report the error or preserve the model and URL.");
                apiKey.Clear();
                await RefreshModels(settings);
                Require(IsMissingKey(status.Text), "Clearing the malformed key did not restore normal credential-free editing.");
                checks.Add("malformed-key-before-valid-url-reports-local-validation-and-preserves-editors");

                phase = "verify-window-resize-controls";
                settings.Width = 760;
                settings.Height = Math.Min(574, SystemParameters.WorkArea.Height - 40);
                advanced.IsExpanded = true;
                url.BringIntoView();
                await Settle(settings);
                var normalModelWidth = model.ActualWidth;
                var normalUrlWidth = url.ActualWidth;
                var maximize = FindAutomationButton(settings, "SettingsMaximizeButton");
                var close = FindAutomationButton(settings, "SettingsCloseButton");
                Require(WindowChrome.GetWindowChrome(settings)?.CaptionHeight > 0 &&
                    WindowChrome.GetIsHitTestVisibleInChrome(maximize) && WindowChrome.GetIsHitTestVisibleInChrome(close),
                    "The native title region or its interactive WPF action buttons are not configured.");
                checks.Add("native-caption-keeps-title-actions-interactive");
                var save = Descendants(settings).OfType<Button>().Single(button => button.Content?.ToString() is "保存" or "Save");
                var beforeResize = DraftSnapshot(settings);
                Save(settings, "normal.png");
                Click(maximize);
                await Settle(settings);
                Require(settings.WindowState == WindowState.Maximized, "The maximize button did not maximize the real settings window.");
                Require(model.ActualWidth > normalModelWidth + 1 && url.ActualWidth > normalUrlWidth + 1,
                    "Maximizing did not enlarge the real model and endpoint editors.");
                Require(FitsInside(save, settings), "The save button is clipped in the maximized window.");
                Save(settings, "maximized.png");
                Require(FitsWorkingArea(save, settings) && FitsWorkingArea(maximize, settings) && FitsWorkingArea(close, settings),
                    "A maximized save or title action extends beyond the monitor working area or behind the taskbar. " +
                    DescribeWindowGeometry(settings, save, maximize, close));
                checks.Add("maximized-actions-fit-physical-monitor-working-area");
                Require(DraftSnapshot(settings) == beforeResize, "Maximizing changed a draft or the default connection.");
                Click(maximize);
                await Settle(settings);
                Require(settings.WindowState == WindowState.Normal && DraftSnapshot(settings) == beforeResize,
                    "Restoring the window lost a draft or changed the default connection.");
                Require(Math.Abs(model.ActualWidth - normalModelWidth) < 1 && Math.Abs(url.ActualWidth - normalUrlWidth) < 1,
                    "Restoring did not restore the editor widths.");
                checks.Add("maximize-and-restore-enlarge-editors-and-preserve-all-drafts-and-default");

                phase = "verify-native-caption-double-click";
                DoubleClickCaption(settings);
                await Settle(settings);
                Require(settings.WindowState == WindowState.Maximized && DraftSnapshot(settings) == beforeResize,
                    "The native title double-click did not maximize while preserving the drafts.");
                DoubleClickCaption(settings);
                await Settle(settings);
                Require(settings.WindowState == WindowState.Normal && DraftSnapshot(settings) == beforeResize,
                    "The native title double-click did not restore while preserving the drafts.");
                checks.Add("native-title-hit-test-and-double-click-maximize-and-restore");

                phase = "verify-600-by-400-window";
                settings.Width = 600;
                settings.Height = 400;
                await Settle(settings);
                Require(FitsInside(save, settings) && FitsInside(maximize, settings) && FitsInside(FindAutomationButton(settings, "SettingsCloseButton"), settings),
                    "The save or title-bar actions are clipped at 600 by 400 DIP.");
                Require(url.ActualWidth > 100 && model.ActualWidth > 100, "The narrow API editors have no usable width.");
                url.BringIntoView();
                await Settle(settings);
                Require(IsInScrollViewport(url, settings), "The API endpoint cannot be scrolled into view at 600 by 400 DIP.");
                Save(settings, "narrow.png");
                checks.Add("600-by-400-window-keeps-title-actions-save-and-scrolled-endpoint-usable");
                Require(JsonSerializer.Serialize(fixture) == original, "Editing changed the host settings instead of isolated drafts.");
                Require(DefaultId(settings) == "qa-settings-first", "The original default was changed without its explicit action.");
                Require(Field<PasswordBox>(settings, "_apiKey").Password.Length == 0, "A synthetic replay unexpectedly acquired an API key.");
                checks.Add("host-settings-and-empty-credentials-remain-unchanged-without-save");
            }
            catch (Exception ex) { failure = $"Phase: {phase}\n{ex}"; Environment.ExitCode = 1; }
            finally
            {
                settings.Close();
                Directory.CreateDirectory(OutputDirectory);
                File.WriteAllText(Path.Combine(OutputDirectory, "result.json"),
                    JsonSerializer.Serialize(new { checks, phase, failure }, new JsonSerializerOptions { WriteIndented = true }),
                    new System.Text.UTF8Encoding(false));
                app.Shutdown(Environment.ExitCode);
            }
        }));
        app.Run(settings);
    }

    private static async Task RefreshModels(SettingsWindow settings)
    {
        // This is the same handler used by the debounce and refresh button. All
        // Fixtures are invalid URLs, HTTPS URLs without authentication, or a
        // deliberately malformed bearer value rejected before SendAsync. Never
        // use a loopback address or a sendable API key/authentication header.
        var operation = (Task)typeof(SettingsWindow).GetMethod("RefreshModelsAsync", Private)!.Invoke(settings, null)!;
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
        await Settle(settings);
    }

    private static async Task ObserveModelStatusAsync(TextBlock status, Action edit, Func<string, bool> completed)
    {
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var property = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        EventHandler handler = (_, _) => { if (completed(status.Text)) changed.TrySetResult(); };
        property.AddValueChanged(status, handler);
        try
        {
            edit();
            if (completed(status.Text)) changed.TrySetResult();
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { property.RemoveValueChanged(status, handler); }
    }

    private static bool IsMissingKey(string text) => text.Contains("输入此提供商的 API Key", StringComparison.Ordinal) ||
        text.Contains("Enter this provider's API key", StringComparison.Ordinal);
    private static string? DefaultId(SettingsWindow settings) => Field<string?>(settings, "_defaultProviderId");
    private static string DraftSnapshot(SettingsWindow settings) => JsonSerializer.Serialize(new
    {
        model = Field<ComboBox>(settings, "_model").Text,
        url = Field<TextBox>(settings, "_baseUrl").Text,
        headers = Field<TextBox>(settings, "_customHeaders").Text,
        parameters = Field<TextBox>(settings, "_requestParameters").Text,
        defaultId = DefaultId(settings)
    });

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static Button FindAutomationButton(DependencyObject root, string id) => Descendants(root).OfType<Button>()
        .Single(button => AutomationProperties.GetAutomationId(button) == id);
    private static Button ConnectionButton(DependencyObject view, string name) => Descendants(view).OfType<Button>()
        .Single(button => button.Content is Grid && Descendants(button).OfType<TextBlock>().Any(label => label.Text == name));

    private static bool FitsInside(FrameworkElement control, FrameworkElement root)
    {
        var bounds = control.TransformToAncestor(root).TransformBounds(new Rect(control.RenderSize));
        return control.IsVisible && bounds.Width >= 24 && bounds.Height >= 24 && bounds.Left >= -.5 && bounds.Top >= -.5 &&
            bounds.Right <= root.ActualWidth + .5 && bounds.Bottom <= root.ActualHeight + .5;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static void DoubleClickCaption(Window window)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var point = window.PointToScreen(new System.Windows.Point(330, 24));
        var position = new IntPtr(unchecked(((int)point.Y << 16) | ((int)point.X & 0xffff)));
        Require(SendMessage(handle, 0x0084 /* WM_NCHITTEST */, IntPtr.Zero, position).ToInt64() == 2 /* HTCAPTION */,
            "The visible title does not participate in the native caption hit test.");
        // Send only to our own synthetic window; no physical cursor or global input.
        SendMessage(handle, 0x00A3 /* WM_NCLBUTTONDBLCLK */, new IntPtr(2), position);
    }

    private static bool FitsWorkingArea(FrameworkElement control, Window window)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var workArea = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var topLeft = control.PointToScreen(new System.Windows.Point(0, 0));
        var bottomRight = control.PointToScreen(new System.Windows.Point(control.ActualWidth, control.ActualHeight));
        const double tolerance = 2;
        return topLeft.X >= workArea.Left - tolerance && topLeft.Y >= workArea.Top - tolerance &&
            bottomRight.X <= workArea.Right + tolerance && bottomRight.Y <= workArea.Bottom + tolerance;
    }

    private static string DescribeWindowGeometry(Window window, FrameworkElement save, FrameworkElement maximize, FrameworkElement close)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var workArea = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(window);
        object Bounds(FrameworkElement control)
        {
            var topLeft = control.PointToScreen(new System.Windows.Point(0, 0));
            var bottomRight = control.PointToScreen(new System.Windows.Point(control.ActualWidth, control.ActualHeight));
            return new { left = topLeft.X, top = topLeft.Y, right = bottomRight.X, bottom = bottomRight.Y };
        }
        return JsonSerializer.Serialize(new
        {
            save = Bounds(save), maximize = Bounds(maximize), close = Bounds(close),
            workingArea = new { left = workArea.Left, top = workArea.Top, right = workArea.Right, bottom = workArea.Bottom },
            window = new { window.ActualWidth, window.ActualHeight, window.Left, window.Top },
            dpi = new { dpi.DpiScaleX, dpi.DpiScaleY, dpi.PixelsPerInchX, dpi.PixelsPerInchY }
        });
    }

    private static bool IsInScrollViewport(FrameworkElement control, DependencyObject root)
    {
        var ancestor = VisualTreeHelper.GetParent(control);
        while (ancestor is not null && ancestor != root)
        {
            if (ancestor is ScrollContentPresenter viewport)
            {
                var bounds = control.TransformToAncestor(viewport).TransformBounds(new Rect(control.RenderSize));
                return bounds.Left >= -.5 && bounds.Top >= -.5 && bounds.Right <= viewport.ActualWidth + .5 && bounds.Bottom <= viewport.ActualHeight + .5;
            }
            ancestor = VisualTreeHelper.GetParent(ancestor);
        }
        return false;
    }

    private static async Task Settle(Window window)
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Save(FrameworkElement element, string fileName)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(OutputDirectory);
        using var stream = File.Create(Path.Combine(OutputDirectory, fileName));
        encoder.Save(stream);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
