// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace mewu_ai_Assistant.Views;

public partial class CaptureOverlayWindow
{
    private bool _syncingDrawingControls;
    private sealed record StrokeStyleDrawingAction(Stroke Stroke, DrawingAttributes Before, DrawingAttributes After) : DrawingAction;
    private sealed record ElementStyleDrawingAction(DrawingElementSpec Before, DrawingElementSpec After) : DrawingAction;

    private DrawingElementSpec? SelectedDrawingElement(SelectionItem item) =>
        _selectedDrawingElementId is { } id ? item.DrawingElements.FirstOrDefault(element => element.Id == id) : null;

    private void ApplySelectedDrawingColor(SelectionItem item)
    {
        if (_selectedDrawingStroke is { } stroke && item.Markup.Strokes.Contains(stroke))
        {
            if (stroke.DrawingAttributes.Color == _drawColor) return;
            var before = stroke.DrawingAttributes.Clone();
            stroke.DrawingAttributes.Color = _drawColor;
            item.DrawingOrder.Add(new StrokeStyleDrawingAction(stroke, before, stroke.DrawingAttributes.Clone()));
            item.DrawingRedo.Clear();
            MarkDrawingChanged(item);
            return;
        }
        var selected = SelectedDrawingElement(item);
        var updated = selected switch
        {
            TextDrawingElement text => (DrawingElementSpec)(text with { Color = _drawColor }),
            NumberDrawingElement number => number with { Color = _drawColor },
            _ => selected
        };
        if (selected is not null && updated is not null) CommitDrawingElementProperty(item, selected, updated);
    }

    private void CommitDrawingElementProperty(SelectionItem item, DrawingElementSpec before, DrawingElementSpec after)
    {
        if (Equals(before, after) || !ReplaceDrawingElement(item, after)) return;
        UpdateDrawingElementVisual(item, after);
        item.DrawingOrder.Add(new ElementStyleDrawingAction(before, after));
        item.DrawingRedo.Clear();
        MarkDrawingChanged(item);
        ShowDrawingObjectSelection(item);
    }

    private static bool ApplyDrawingElementStyle(SelectionItem item, DrawingElementSpec style)
    {
        var current = item.DrawingElements.FirstOrDefault(element => element.Id == style.Id);
        var updated = (current, style) switch
        {
            (TextDrawingElement text, TextDrawingElement values) => (DrawingElementSpec)(text with
            {
                FontFamily = values.FontFamily, FontSize = values.FontSize,
                Color = values.Color, Highlight = values.Highlight
            }),
            (NumberDrawingElement number, NumberDrawingElement values) => number with { Color = values.Color },
            _ => null
        };
        return updated is not null && ReplaceDrawingElement(item, updated);
    }

    private void SyncDrawingPropertyControls(SelectionItem item)
    {
        if (_syncingDrawingControls) return;
        _syncingDrawingControls = true;
        try
        {
            var selected = SelectedDrawingElement(item);
            if (selected is TextDrawingElement text)
            {
                _drawColor = text.Color;
                _drawFontFamily = text.FontFamily;
                _drawFontSize = text.FontSize;
                _drawTextHighlight = text.Highlight;
            }
            else if (selected is NumberDrawingElement number) _drawColor = number.Color;
            else if (_selectedDrawingStroke is { } stroke && item.Markup.Strokes.Contains(stroke)) _drawColor = stroke.DrawingAttributes.Color;
            DrawingTextControls.Visibility = selected is TextDrawingElement || _drawTool == DrawTool.Text ? Visibility.Visible : Visibility.Collapsed;
            EnsureDrawingControls();
            ApplyCurrentDrawingAttributes(item);
            if (_drawingMode && DrawingToolbar.Visibility == Visibility.Visible) PositionFloatingBar(DrawingToolbar, item);
        }
        finally { _syncingDrawingControls = false; }
    }

    private void FocusDrawingText(SelectionItem item, TextBox editor)
    {
        if (!_drawingMode || editor.Tag is not Guid id || !item.DrawingElements.Any(element => element.Id == id)) return;
        _selectedDrawingElementId = id;
        _selectedDrawingStroke = null;
        editor.BorderBrush = new SolidColorBrush(Color.FromRgb(108, 124, 238));
        SyncDrawingPropertyControls(item);
    }

    private void DrawingTextLostFocus(SelectionItem item, TextBox editor)
    {
        editor.BorderBrush = Brushes.Transparent;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_closed || !_selections.Contains(item) || editor.IsKeyboardFocusWithin || _drawingModalOpen ||
                DrawingFontFamily.IsDropDownOpen || DrawingFontSize.IsDropDownOpen) return;
            // A toolbar interaction keeps the editing target, including a new text draft.
            if (Keyboard.FocusedElement is DependencyObject focus && IsInside(focus, DrawingToolbar)) return;
            if (editor.Tag is Guid id && ReferenceEquals(FindDrawingElementVisual(item, id), editor) &&
                item.DrawingElements.Any(element => element.Id == id &&
                element is TextDrawingElement text && string.IsNullOrWhiteSpace(text.Text))) RemoveEmptyDrawingText(item);
        }));
    }

    private static int NextAvailableDrawingNumber(SelectionItem item)
    {
        var used = item.DrawingElements.OfType<NumberDrawingElement>().Select(element => element.Number).ToHashSet();
        for (var candidate = 1; candidate <= used.Count; candidate++)
            if (!used.Contains(candidate)) return candidate;
        return used.Count + 1;
    }

    private static double DrawingNumberFontSize(NumberDrawingElement number)
    {
        var digits = number.Number.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        return Math.Clamp(number.Diameter * Math.Min(.68, 1.08 / digits), 6, 80);
    }

    private static bool DrawingActionTargets(DrawingAction action, HashSet<Guid> ids) => action switch
    {
        ElementDrawingAction created => ids.Contains(created.Element.Id),
        ElementRemovalDrawingAction removed => ids.Contains(removed.Element.Id),
        ElementMoveDrawingAction changed => ids.Contains(changed.Before.Id),
        ElementStyleDrawingAction styled => ids.Contains(styled.Before.Id),
        _ => false
    };
}
