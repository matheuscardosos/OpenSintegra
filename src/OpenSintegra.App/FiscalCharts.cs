using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using OpenSintegra.Core;

namespace OpenSintegra.App;

/// <summary>
/// Custom vector grouped bar chart for monthly Entradas vs Saidas comparison.
/// </summary>
public sealed class MonthlyBarChartControl : Control
{
    private IReadOnlyList<MonthlyBalanceRow> _rows = Array.Empty<MonthlyBalanceRow>();
    private string? _highlightedYm;
    private int _hoveredIndex = -1;

    private static readonly IBrush BlueBrush = Brush.Parse("#2563EB");
    private static readonly IBrush GreenBrush = Brush.Parse("#16A34A");
    private static readonly IBrush BlueMutedBrush = Brush.Parse("#93C5FD");
    private static readonly IBrush GreenMutedBrush = Brush.Parse("#86EFAC");
    private static readonly Pen GridPen = new(Brush.Parse("#E2E8F0"), 1);
    private static readonly IBrush AxisTextBrush = Brush.Parse("#64748B");
    private static readonly IBrush DarkTextBrush = Brush.Parse("#0F172A");

    public void SetData(IReadOnlyList<MonthlyBalanceRow> rows, string? highlightedYm = null)
    {
        _rows = rows;
        _highlightedYm = highlightedYm;
        _hoveredIndex = -1;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_rows.Count == 0) return;

        var pos = e.GetPosition(this);
        double leftPad = 62;
        double chartW = Math.Max(40, Bounds.Width - leftPad - 12);
        double slotW = chartW / _rows.Count;
        int idx = pos.X >= leftPad ? (int)((pos.X - leftPad) / slotW) : -1;
        if (idx < 0 || idx >= _rows.Count) idx = -1;

        if (idx != _hoveredIndex)
        {
            _hoveredIndex = idx;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredIndex != -1)
        {
            _hoveredIndex = -1;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w < 120 || h < 80) return;

        double leftPad = 62;
        double topPad = 14;
        double bottomPad = 28;
        double rightPad = 10;
        double plotW = Math.Max(40, w - leftPad - rightPad);
        double plotH = Math.Max(40, h - topPad - bottomPad);

        long maxVal = 100_000;
        foreach (var r in _rows)
            maxVal = Math.Max(maxVal, Math.Max(r.TotalEntradasCents, r.TotalSaidasCents));

        // Round ceiling to nice thousand step
        double maxReais = maxVal / 100.0;
        double stepReais = Math.Max(5_000, Math.Ceiling(maxReais / 4.0 / 5_000.0) * 5_000.0);
        double topScale = stepReais * 4.0;

        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);

        // Horizontal gridlines & Y-axis labels
        for (int i = 0; i <= 4; i++)
        {
            double gy = topPad + plotH - (plotH * i / 4.0);
            ctx.DrawLine(GridPen, new Point(leftPad, gy), new Point(leftPad + plotW, gy));

            double valReais = stepReais * i;
            string lbl = valReais == 0 ? "R$ 0" : $"R$ {valReais / 1000.0:N0} mil";
            var ft = new FormattedText(lbl, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, typeface, 10.5, AxisTextBrush);
            ctx.DrawText(ft, new Point(Math.Max(0, leftPad - ft.Width - 8), gy - ft.Height / 2));
        }

        if (_rows.Count == 0) return;

        double slotW = plotW / _rows.Count;
        double barPairW = Math.Min(34, slotW * 0.66);
        double barW = Math.Max(5, (barPairW - 3) / 2.0);

        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            bool dim = !string.IsNullOrEmpty(_highlightedYm) && _highlightedYm != r.YearMonth;
            bool isHover = i == _hoveredIndex;

            double slotX = leftPad + i * slotW;
            if (isHover)
            {
                ctx.FillRectangle(Brush.Parse("#F1F5F9"), new Rect(slotX + 2, topPad, slotW - 4, plotH), 4);
            }

            double centerX = slotX + slotW / 2.0;
            double entH = Math.Max(2, (r.TotalEntradasCents / 100.0) / topScale * plotH);
            double saiH = Math.Max(2, (r.TotalSaidasCents / 100.0) / topScale * plotH);
            if (r.TotalEntradasCents == 0) entH = 0;
            if (r.TotalSaidasCents == 0) saiH = 0;

            double xEnt = centerX - barW - 1.5;
            double xSai = centerX + 1.5;

            if (entH > 0)
            {
                ctx.FillRectangle(dim ? BlueMutedBrush : BlueBrush,
                    new Rect(xEnt, topPad + plotH - entH, barW, entH), 3);
            }
            if (saiH > 0)
            {
                ctx.FillRectangle(dim ? GreenMutedBrush : GreenBrush,
                    new Rect(xSai, topPad + plotH - saiH, barW, saiH), 3);
            }

            string mLabel = FormatShortMonth(r.YearMonth);
            var ftM = new FormattedText(mLabel, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                isHover ? new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold) : typeface,
                10.5, isHover ? DarkTextBrush : AxisTextBrush);
            ctx.DrawText(ftM, new Point(centerX - ftM.Width / 2, topPad + plotH + 6));
        }

        if (_hoveredIndex >= 0 && _hoveredIndex < _rows.Count)
        {
            var hr = _rows[_hoveredIndex];
            string tip = $"{hr.MonthLabel}  |  Entradas: R$ {TaxIdValidator.FormatCentsBrl(hr.TotalEntradasCents)}  |  Saidas: R$ {TaxIdValidator.FormatCentsBrl(hr.TotalSaidasCents)}";
            var ftTip = new FormattedText(tip, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, typeface, 11, DarkTextBrush);
            double tx = Math.Clamp(leftPad + _hoveredIndex * slotW, leftPad, leftPad + plotW - ftTip.Width - 16);
            var tipRect = new Rect(tx, 2, ftTip.Width + 14, 22);
            ctx.DrawRectangle(Brushes.White, new Pen(Brush.Parse("#CBD5E1"), 1), tipRect, 4, 4);
            ctx.DrawText(ftTip, new Point(tx + 7, 5));
        }
    }

    private static string FormatShortMonth(string ym)
    {
        if (ym.Length != 6 || !int.TryParse(ym[4..6], out int m)) return ym;
        string[] names = ["", "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez"];
        string mName = m is >= 1 and <= 12 ? names[m] : ym[4..6];
        return $"{mName}/{ym[2..4]}";
    }
}

/// <summary>
/// Custom vector Donut Chart displaying Entradas vs Saidas composition of the selected scope.
/// </summary>
public sealed class CompositionDonutControl : Control
{
    private long _entradasCents;
    private long _saidasCents;

    public void SetData(long entradasCents, long saidasCents)
    {
        _entradasCents = Math.Max(0, entradasCents);
        _saidasCents = Math.Max(0, saidasCents);
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w < 80 || h < 80) return;

        double size = Math.Min(w, h) - 10;
        double cx = w / 2.0;
        double cy = h / 2.0;
        double outerR = size / 2.0;
        double innerR = outerR * 0.62;
        double strokeW = outerR - innerR;
        double midR = (outerR + innerR) / 2.0;

        long total = _entradasCents + _saidasCents;
        var greenPen = new Pen(Brush.Parse("#16A34A"), strokeW, lineCap: PenLineCap.Flat);
        var bluePen = new Pen(Brush.Parse("#2563EB"), strokeW, lineCap: PenLineCap.Flat);
        var emptyPen = new Pen(Brush.Parse("#E2E8F0"), strokeW);

        if (total <= 0)
        {
            ctx.DrawEllipse(null, emptyPen, new Point(cx, cy), midR, midR);
        }
        else
        {
            double saiFrac = _saidasCents / (double)total;
            double entFrac = _entradasCents / (double)total;

            // Draw segments cleanly using PathGeometry arcs (-90 deg top start)
            if (saiFrac >= 0.999)
            {
                ctx.DrawEllipse(null, greenPen, new Point(cx, cy), midR, midR);
            }
            else if (entFrac >= 0.999)
            {
                ctx.DrawEllipse(null, bluePen, new Point(cx, cy), midR, midR);
            }
            else
            {
                double startDeg = -90.0;
                double saiDeg = saiFrac * 360.0 - 2.0;
                double entDeg = entFrac * 360.0 - 2.0;

                if (saiDeg > 1)
                    DrawArc(ctx, greenPen, cx, cy, midR, startDeg, startDeg + saiDeg);
                if (entDeg > 1)
                    DrawArc(ctx, bluePen, cx, cy, midR, startDeg + saiDeg + 2.0, startDeg + saiDeg + 2.0 + entDeg);
            }
        }

        string centerMoney = $"R$ {TaxIdValidator.FormatCentsBrl(total)}";
        var tfBold = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
        var tfReg = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);

        double fontSize = total >= 100_000_000 ? 11.5 : 13.0;
        var ftVal = new FormattedText(centerMoney, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, tfBold, fontSize, Brush.Parse("#0F172A"));
        var ftSub = new FormattedText("Movimentado", CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, tfReg, 10.5, Brush.Parse("#64748B"));

        ctx.DrawText(ftVal, new Point(cx - ftVal.Width / 2, cy - ftVal.Height + 2));
        ctx.DrawText(ftSub, new Point(cx - ftSub.Width / 2, cy + 2));
    }

    private static void DrawArc(DrawingContext ctx, Pen pen, double cx, double cy, double r, double startDeg, double endDeg)
    {
        double a1 = startDeg * Math.PI / 180.0;
        double a2 = endDeg * Math.PI / 180.0;
        var p1 = new Point(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1));
        var p2 = new Point(cx + r * Math.Cos(a2), cy + r * Math.Sin(a2));
        bool large = (endDeg - startDeg) > 180.0;

        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(p1, false);
            g.ArcTo(p2, new Size(r, r), 0, large, SweepDirection.Clockwise);
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, geo);
    }
}
