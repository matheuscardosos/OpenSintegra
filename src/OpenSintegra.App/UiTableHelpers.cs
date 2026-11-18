using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace OpenSintegra.App;

/// <summary>
/// Shared virtualized ListBox helpers with safe container recycling via DataContextChanged
/// and pixel-accurate header alignment synchronized with PART_ScrollViewer viewport bounds.
/// </summary>
internal static class UiTableHelpers
{
    internal static readonly IBrush BrushBorderLight = Brush.Parse("#E2E8F0");
    internal static readonly IBrush BrushHeaderBg = Brush.Parse("#F1F5F9");
    internal static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Thickness CellPad = new(8, 6);

    internal static Grid BuildRowGrid(GridLength[] cols)
    {
        var g = new Grid();
        foreach (var c in cols)
            g.ColumnDefinitions.Add(new ColumnDefinition(c));
        return g;
    }

    internal static ListBox CreateRecycledListBox<T>(
        IReadOnlyList<T> items,
        GridLength[] cols,
        Action<Grid, Control?[], T> bindRow,
        double? listHeight = null,
        Action<Grid, Control?[]>? initRowSlots = null,
        Action<T>? onActivateRow = null,
        Thickness? borderThickness = null,
        IBrush? borderBrush = null)
    {
        var listBox = new ListBox
        {
            ItemsSource = items,
            Background = Brushes.White,
            BorderThickness = borderThickness ?? new Thickness(0),
            BorderBrush = borderBrush ?? BrushBorderLight
        };
        if (listHeight.HasValue)
            listBox.Height = listHeight.Value;

        ScrollViewer.SetIsScrollChainingEnabled(listBox, true);

        listBox.ItemTemplate = new FuncDataTemplate<T>((initialItem, _) =>
        {
            var g = BuildRowGrid(cols);
            g.Background = Brushes.Transparent;
            var slots = new Control?[cols.Length];
            g.Tag = slots;

            initRowSlots?.Invoke(g, slots);

            var wrapper = new Border
            {
                BorderBrush = BrushBorderLight,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = g
            };

            if (onActivateRow is not null)
            {
                g.Cursor = HandCursor;
                g.Tapped += (_, e) =>
                {
                    if (IsDescendantOfButton(e.Source as Control, g))
                        return;
                    if (g.DataContext is T current)
                    {
                        e.Handled = true;
                        onActivateRow(current);
                    }
                };
            }

            void ApplyCurrent(object? ctx)
            {
                if (ctx is T current)
                    bindRow(g, slots, current);
            }

            g.DataContextChanged += (_, _) => ApplyCurrent(g.DataContext);
            if (initialItem is not null)
                bindRow(g, slots, initialItem);

            return wrapper;
        }, supportsRecycling: true);

        listBox.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                new Setter(Layoutable.MarginProperty, new Thickness(0)),
                new Setter(Layoutable.MinHeightProperty, 0.0),
                new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)
            }
        });

        if (onActivateRow is not null)
        {
            listBox.KeyDown += (_, e) =>
            {
                bool activateKey = e.Key is Key.Enter or Key.Return ||
                                   (e.Key == Key.Space && e.Source is not Button);
                if (activateKey && listBox.SelectedItem is T selected)
                {
                    e.Handled = true;
                    onActivateRow(selected);
                }
            };
        }

        return listBox;
    }

    internal static void ConfigureModalShortcuts(Window dlg, Control? initialFocus = null, Action? onEscape = null)
    {
        dlg.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (onEscape is not null)
                    onEscape();
                else
                    dlg.Close();
            }
        };
        if (initialFocus is not null)
            dlg.Opened += (_, _) => initialFocus.Focus();
    }

    private static bool IsDescendantOfButton(Control? src, Control stopAt)
    {
        for (var cur = src; cur is not null && !ReferenceEquals(cur, stopAt); cur = cur.Parent as Control)
        {
            if (cur is Button) return true;
        }
        return false;
    }

    internal static void SetCellSlot(
        Grid g,
        Control?[] slots,
        int col,
        string text,
        TextAlignment align,
        FontWeight weight,
        IBrush fg,
        double fontSize = 11.5,
        Thickness? padding = null)
    {
        if (slots[col] is TextBlock tb)
        {
            tb.Text = text;
            tb.FontWeight = weight;
            tb.Foreground = fg;
            ToolTip.SetTip(tb, string.IsNullOrWhiteSpace(text) ? null : text);
            return;
        }

        var created = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = fg,
            TextAlignment = align,
            Padding = padding ?? CellPad,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (!string.IsNullOrWhiteSpace(text))
            ToolTip.SetTip(created, text);
        Grid.SetColumn(created, col);
        slots[col] = created;
        g.Children.Add(created);
    }

    internal static void AttachSyncedHeaderPadding(ListBox listBox, Grid headerGrid)
    {
        void SyncFromScrollViewer(ScrollViewer sv)
        {
            double gap = Math.Max(0, sv.Bounds.Width - sv.Viewport.Width);
            var target = new Thickness(0, 0, gap, 0);
            if (!headerGrid.Margin.Equals(target))
                headerGrid.Margin = target;
        }

        listBox.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<ScrollViewer>("PART_ScrollViewer") is { } sv)
            {
                sv.PropertyChanged += (_, args) =>
                {
                    if (args.Property == ScrollViewer.ViewportProperty ||
                        args.Property == ScrollViewer.ExtentProperty ||
                        args.Property == Visual.BoundsProperty)
                    {
                        SyncFromScrollViewer(sv);
                    }
                };
                SyncFromScrollViewer(sv);
            }
        };
    }

    internal static Control WrapWithHeader(GridLength[] cols, Action<Grid> buildHeader, ListBox listBox)
    {
        var outer = new Border
        {
            BorderBrush = BrushBorderLight,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true
        };

        var hdr = BuildRowGrid(cols);
        hdr.Background = BrushHeaderBg;
        buildHeader(hdr);
        AttachSyncedHeaderPadding(listBox, hdr);

        var stack = new StackPanel();
        stack.Children.Add(new Border
        {
            Background = BrushHeaderBg,
            BorderBrush = BrushBorderLight,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = hdr
        });
        stack.Children.Add(listBox);
        outer.Child = stack;
        return outer;
    }
}
