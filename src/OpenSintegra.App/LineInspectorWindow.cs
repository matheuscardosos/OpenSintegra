using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OpenSintegra.Core;

namespace OpenSintegra.App;

public sealed class LineInspectorWindow : Window
{
    public LineInspectorWindow(RawLine line, int highlightStartCol = 1, int highlightEndCol = 2)
    {
        Title = $"Detalhes da Linha {line.LineNumber} (Registro {line.RecordType})";
        Icon = VectorIcons.AppWindowIcon;
        Width = 820;
        Height = 560;
        MinWidth = 680;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#F8FAFC");

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(20)
        };

        // Top: Raw line preview card
        var topCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var topStack = new StackPanel { Spacing = 8 };
        topStack.Children.Add(new TextBlock
        {
            Text = $"Linha {line.LineNumber} do arquivo  |  Registro Tipo {line.RecordType}  |  {line.CharLen} caracteres",
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            Foreground = Brush.Parse("#0F172A")
        });

        string extracted = SintegraParser.SliceCols(line.RawText, highlightStartCol, highlightEndCol);
        topStack.Children.Add(new TextBlock
        {
            Text = $"Trecho destacado (colunas {highlightStartCol} a {highlightEndCol}): \"{extracted}\"",
            FontSize = 13,
            Foreground = Brush.Parse("#2563EB")
        });

        var rawBox = new TextBox
        {
            Text = line.RawText,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, monospace"),
            FontSize = 12.5,
            Background = Brush.Parse("#F1F5F9"),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush.Parse("#CBD5E1"),
            Padding = new Thickness(10, 8)
        };
        topStack.Children.Add(rawBox);
        topCard.Child = topStack;
        Grid.SetRow(topCard, 0);
        root.Children.Add(topCard);

        // Middle: Scrollable list of decomposed fields
        var fieldsCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            ClipToBounds = true
        };

        var scroll = new ScrollViewer();
        var fieldsList = new StackPanel { Spacing = 0 };

        // Header row
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("90,*,180,180"),
            Background = Brush.Parse("#F1F5F9"),
            
        };
        var headerBorder = new Border
        {
            Background = Brush.Parse("#F1F5F9"),
            BorderBrush = Brush.Parse("#E2E8F0"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10),
            Child = headerGrid
        };
        headerGrid.Children.Add(CreateCell("Colunas", true, 0));
        headerGrid.Children.Add(CreateCell("Campo (Convênio ICMS 57/95)", true, 1));
        headerGrid.Children.Add(CreateCell("Valor Leitura", true, 2));
        headerGrid.Children.Add(CreateCell("No Arquivo (Bruto)", true, 3));
        fieldsList.Children.Add(headerBorder);

        var slices = RecordSchemaDecomposer.DecomposeLine(line.RawText);
        foreach (var s in slices)
        {
            bool isHighlighted = highlightStartCol >= s.StartCol && highlightStartCol <= s.EndCol;
            var rowGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("90,*,180,180")
            };
            var rowBorder = new Border
            {
                Background = isHighlighted ? Brush.Parse("#EFF6FF") : Brushes.White,
                BorderBrush = Brush.Parse("#F1F5F9"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 9),
                Child = rowGrid
            };

            rowGrid.Children.Add(CreateCell($"{s.StartCol:000} a {s.EndCol:000}", isHighlighted, 0, mono: true));
            rowGrid.Children.Add(CreateCell(s.Name, isHighlighted, 1));
            rowGrid.Children.Add(CreateCell(s.FormattedValue, isHighlighted, 2));
            rowGrid.Children.Add(CreateCell(s.RawValue, isHighlighted, 3, mono: true));
            fieldsList.Children.Add(rowBorder);
        }

        scroll.Content = fieldsList;
        fieldsCard.Child = scroll;
        Grid.SetRow(fieldsCard, 1);
        root.Children.Add(fieldsCard);

        // Bottom: Close button
        var bottomPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var btnClose = new Button
        {
            Content = "Fechar",
            Padding = new Thickness(20, 8),
            CornerRadius = new CornerRadius(6)
        };
        btnClose.Click += (_, _) => Close();
        bottomPanel.Children.Add(btnClose);
        Grid.SetRow(bottomPanel, 2);
        root.Children.Add(bottomPanel);

        Content = root;
        UiTableHelpers.ConfigureModalShortcuts(this, btnClose);
    }

    private static TextBlock CreateCell(string text, bool bold, int col, bool mono = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
            FontSize = 13,
            Foreground = Brush.Parse("#0F172A"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (!string.IsNullOrWhiteSpace(text))
            ToolTip.SetTip(tb, text);
        if (mono)
            tb.FontFamily = new FontFamily("Consolas, Cascadia Mono, monospace");
        Grid.SetColumn(tb, col);
        return tb;
    }
}
