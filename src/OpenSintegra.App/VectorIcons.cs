using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace OpenSintegra.App;

/// <summary>
/// Crisp 24x24 Fluent-style vector geometry glyphs for toolbars, sidebars and KPI cards.
/// </summary>
public static class VectorIcons
{
    public const string Home = "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z";
    public const string FolderOpen = "M20 6h-8l-2-2H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 12H4V8h16v10z";
    public const string UploadZip = "M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z";
    public const string CheckBadge = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z";
    public const string Wrench = "M22.7 19l-9.1-9.1c.9-2.3.4-5-1.5-6.9-2-2-5-2.4-7.4-1.3L9 6 6 9 1.6 4.7C.4 7.1.9 10.1 2.9 12.1c1.9 1.9 4.6 2.4 6.9 1.5l9.1 9.1c.4.4 1 .4 1.4 0l2.3-2.3c.5-.4.5-1.1.1-1.4z";
    public const string ChartBar = "M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8z";
    public const string DocumentText = "M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z";
    public const string ExportDoc = "M19 12v7H5v-7H3v7c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2v-7h-2zm-6 .67l2.59-2.58L17 11.5l-5 5-5-5 1.41-1.41L11 12.67V3h2v9.67z";
    public const string SettingsGear = "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z";
    public const string Building = "M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z";
    public const string ArrowDown = "M20 12l-1.41-1.41L13 16.17V4h-2v12.17l-5.58-5.59L4 12l8 8 8-8z";
    public const string ArrowUp = "M4 12l1.41 1.41L11 7.83V20h2V7.83l5.58 5.59L20 12l-8-8-8 8z";
    public const string Scale = "M12 3c-.83 0-1.5.67-1.5 1.5 0 .44.19.84.5 1.11V7H5.5v2h1.61l-3.21 7h5.2l-3.21-7h5.11v9.5c-2.26.25-4 2.18-4 4.5h10c0-2.32-1.74-4.25-4-4.5V9h5.11l-3.21 7h5.2l-3.21-7H18.5V7H13V5.61c.31-.27.5-.67.5-1.11 0-.83-.67-1.5-1.5-1.5zm-5.5 9l1.39-3h-2.78l1.39 3zm11 0l1.39-3h-2.78l1.39 3z";
    public const string Warning = "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z";
    public const string Database = "M12 3C7.58 3 4 4.79 4 7s3.58 4 8 4 8-1.79 8-4-3.58-4-8-4zM4 9v3c0 2.21 3.58 4 8 4s8-1.79 8-4V9c0 2.21-3.58 4-8 4s-8-1.79-8-4zm0 5v3c0 2.21 3.58 4 8 4s8-1.79 8-4v-3c0 2.21-3.58 4-8 4s-8-1.79-8-4z";
    public const string Split = "M14 4l2.29 2.29-2.88 2.88 1.42 1.42 2.88-2.88L20 10V4zm-4 0H4v6l2.29-2.29 4.71 4.7V20h2v-8.41l-5.29-5.3z";
    public const string Compare = "M9.01 14H2v2h7.01v3L13 15l-3.99-4v3zm5.98-1v-3H22V8h-7.01V5L11 9l3.99 4z";
    public const string Refresh = "M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z";

    public static WindowIcon? AppWindowIcon { get; } = LoadEmbeddedWindowIcon();

    private static WindowIcon? LoadEmbeddedWindowIcon()
    {
        try
        {
            using var stream = typeof(VectorIcons).Assembly.GetManifestResourceStream("OpenSintegra.Assets.app-icon.png");
            return stream is null ? null : new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }

    public static PathIcon Create(string pathData, string hexColor, double size = 18)
    {
        var icon = new PathIcon
        {
            Data = Geometry.Parse(pathData),
            Foreground = Brush.Parse(hexColor),
            Width = size,
            Height = size
        };
        AutomationProperties.SetAccessibilityView(icon, AccessibilityView.Raw);
        return icon;
    }

    /// <summary>
    /// Renders the official OpenSintegra vector monogram (matching assets/brand/opensintegra-mark.svg).
    /// </summary>
    public static Control CreateBrandMark(double size = 34, string colorHex = "#0066FC")
    {
        var stroke = Brush.Parse(colorHex);
        var canvas = new Canvas { Width = 100, Height = 100 };

        Avalonia.Controls.Shapes.Path MakeStrokePath(string data) => new()
        {
            Data = Geometry.Parse(data),
            Stroke = stroke,
            StrokeThickness = 8.5,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round
        };

        canvas.Children.Add(MakeStrokePath("M 50 16.6 L 82.2 36.1 L 50 55.6 L 17.8 36.1 Z"));
        canvas.Children.Add(MakeStrokePath("M 17.8 50.0 L 29.2 56.9"));
        canvas.Children.Add(MakeStrokePath("M 70.8 56.9 L 82.2 50.0"));
        canvas.Children.Add(MakeStrokePath("M 17.8 63.9 L 50 83.4 L 82.2 63.9"));

        return new Viewbox
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Child = canvas
        };
    }
}
