using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace OpenSintegra.App;

/// <summary>
/// Modal progress window matching traditional desktop accounting software import UX,
/// keeping the UI responsive while processing large Sintegra/XML files in background tasks.
/// </summary>
public sealed class ImportProgressWindow : Window
{
    private readonly ProgressBar _progressBar = new();
    private readonly TextBlock _lblStatus = new();
    private readonly TextBlock _lblPercent = new();
    private readonly CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    public ImportProgressWindow(string title = "Importando Registro")
    {
        Title = title;
        Icon = VectorIcons.AppWindowIcon;
        Width = 460;
        Height = 205;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#FFFFFF");

        var root = new StackPanel
        {
            Margin = new Thickness(24, 22),
            Spacing = 14
        };

        var headerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var iconBox = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(8),
            Background = Brush.Parse("#EFF6FF"),
            BorderBrush = Brush.Parse("#BFDBFE"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 14, 0),
            Child = VectorIcons.Create(VectorIcons.DocumentText, "#2563EB", 24)
        };
        Grid.SetColumn(iconBox, 0);
        headerRow.Children.Add(iconBox);

        var textStack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock
        {
            Text = "Importando registro...",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = Brush.Parse("#0F172A")
        });
        _lblStatus.Text = "Preparando leitura dos dados...";
        _lblStatus.FontSize = 12.5;
        _lblStatus.Foreground = Brush.Parse("#475569");
        _lblStatus.TextTrimming = TextTrimming.CharacterEllipsis;
        textStack.Children.Add(_lblStatus);

        Grid.SetColumn(textStack, 1);
        headerRow.Children.Add(textStack);
        root.Children.Add(headerRow);

        var progGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Value = 5;
        _progressBar.Height = 14;
        _progressBar.Foreground = Brush.Parse("#16A34A");
        _progressBar.Background = Brush.Parse("#E2E8F0");
        _progressBar.CornerRadius = new CornerRadius(4);
        _progressBar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_progressBar, 0);
        progGrid.Children.Add(_progressBar);

        _lblPercent.Text = "5%";
        _lblPercent.FontSize = 13;
        _lblPercent.FontWeight = FontWeight.SemiBold;
        _lblPercent.Foreground = Brush.Parse("#334155");
        _lblPercent.Margin = new Thickness(12, 0, 0, 0);
        _lblPercent.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_lblPercent, 1);
        progGrid.Children.Add(_lblPercent);

        root.Children.Add(progGrid);

        var btnCancel = new Button
        {
            Content = "Cancelar",
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            Foreground = Brush.Parse("#334155"),
            Padding = new Thickness(18, 7),
            CornerRadius = new CornerRadius(5)
        };
        btnCancel.Click += (_, _) =>
        {
            btnCancel.IsEnabled = false;
            btnCancel.Content = "Cancelando...";
            _cts.Cancel();
        };
        root.Children.Add(btnCancel);

        Content = root;
    }

    public void UpdateProgress(int percent, string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            int clamped = Math.Clamp(percent, 0, 100);
            _progressBar.Value = clamped;
            _lblPercent.Text = $"{clamped}%";
            _lblStatus.Text = status;
        });
    }
}
