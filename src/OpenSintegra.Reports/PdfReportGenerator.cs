using OpenSintegra.Core;
using SkiaSharp;

namespace OpenSintegra.Reports;

/// <summary>
/// Generates clear, client-friendly A4 vector PDF accounting reports using SkiaSharp (MIT license).
/// </summary>
public static class PdfReportGenerator
{
    private const float PageW = 595f;
    private const float PageH = 842f;
    private const float Margin = 36f;
    private const float ContentW = PageW - Margin * 2f;

    public static void GeneratePdf(FiscalReportContext ctx, string outputPath)
    {
        using var stream = File.Create(outputPath);
        using var document = SKDocument.CreatePdf(stream);

        using var tfReg = ResolveTypeface(false);
        using var tfBold = ResolveTypeface(true);

        using var paintDark = new SKPaint { Color = SKColor.Parse("#0F172A"), IsAntialias = true };
        using var paintMuted = new SKPaint { Color = SKColor.Parse("#475569"), IsAntialias = true };
        using var paintGreen = new SKPaint { Color = SKColor.Parse("#15803D"), IsAntialias = true };
        using var paintRed = new SKPaint { Color = SKColor.Parse("#B91C1C"), IsAntialias = true };
        using var paintBlue = new SKPaint { Color = SKColor.Parse("#1D4ED8"), IsAntialias = true };
        using var paintLine = new SKPaint { Color = SKColor.Parse("#CBD5E1"), StrokeWidth = 0.8f, IsStroke = true };
        using var paintHeavyLine = new SKPaint { Color = SKColor.Parse("#1E293B"), StrokeWidth = 1.4f, IsStroke = true };
        using var paintFillHeader = new SKPaint { Color = SKColor.Parse("#E2E8F0"), IsStroke = false };
        using var paintFillAlt = new SKPaint { Color = SKColor.Parse("#F8FAFC"), IsStroke = false };
        using var paintFillGreenTint = new SKPaint { Color = SKColor.Parse("#ECFDF5"), IsStroke = false };
        using var paintFillRedTint = new SKPaint { Color = SKColor.Parse("#FEF2F2"), IsStroke = false };
        using var paintWhite = new SKPaint { Color = SKColors.White, IsStroke = false };

        var v = ctx.ScopedView;
        var r10 = ctx.Document.Reg10;
        string timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        int pageNum = 0;

        string reportTitle = ctx.Mode switch
        {
            ReportMode.SomenteNotasNaoLancadas => "Notas Fiscais Não Lançadas no Sintegra",
            ReportMode.ProdutosMaisVendidos => "Ranking de Produtos Mais Vendidos",
            _ => "Relatório Fiscal de Entradas e Saídas"
        };

        SKCanvas? canvas = null;
        float y = 0;

        void StartNewPage()
        {
            if (canvas is not null)
            {
                DrawFooter(canvas, tfReg, paintMuted, paintLine, ctx.AppVersion, timestamp, pageNum);
                document.EndPage();
            }
            pageNum++;
            canvas = document.BeginPage(PageW, PageH);
            canvas.Clear(SKColors.White);
            y = Margin;
            DrawHeader(canvas, tfReg, tfBold, paintDark, paintMuted, paintHeavyLine, reportTitle, v.PeriodLabel);
            y = Margin + 44f;
        }

        void EnsureSpace(float needed)
        {
            if (canvas is null || y + needed > PageH - Margin - 28f)
                StartNewPage();
        }

        StartNewPage();

        // 1. Company Identification section
        y += 8f;
        DrawSectionBand(canvas!, ref y, "Identificação da Empresa", tfBold, paintDark, paintFillHeader);
        y += 10f;

        string companyName = r10?.NomeContribuinte ?? "Contribuinte não identificado";
        string cnpj = r10 is null ? "-" : TaxIdValidator.FormatCnpj(r10.Cnpj);
        string ie = r10?.Ie ?? "-";

        DrawText(canvas!, "Razão Social:", Margin, y, tfBold, 9.5f, paintDark);
        DrawText(canvas!, TrimToLen(companyName, 44), Margin + 74f, y, tfReg, 9.5f, paintDark);
        DrawText(canvas!, "Regime Tributário:", Margin + 320f, y, tfBold, 9.5f, paintDark);
        DrawText(canvas!, ctx.TaxRegime, Margin + 412f, y, tfReg, 9.5f, paintDark);
        y += 16f;

        DrawText(canvas!, "CNPJ:", Margin, y, tfBold, 9.5f, paintDark);
        DrawText(canvas!, cnpj, Margin + 74f, y, tfReg, 9.5f, paintDark);
        DrawText(canvas!, "IE:", Margin + 205f, y, tfBold, 9.5f, paintDark);
        DrawText(canvas!, ie, Margin + 225f, y, tfReg, 9.5f, paintDark);
        DrawText(canvas!, "Convênio:", Margin + 320f, y, tfBold, 9.5f, paintDark);
        DrawText(canvas!, "ICMS 57/95 (Sintegra)", Margin + 375f, y, tfReg, 9.5f, paintDark);
        y += 20f;

        // 2. Summary Box (4 clean, easy-to-understand columns)
        DrawSectionBand(canvas!, ref y, "Resumo do Período", tfBold, paintDark, paintFillHeader);
        y += 8f;

        float boxH = 46f;
        float[] colWs = [124f, 124f, 134f, ContentW - 382f];
        var boxRect = new SKRect(Margin, y, Margin + ContentW, y + boxH);
        canvas!.DrawRoundRect(boxRect, 4f, 4f, paintWhite);

        bool saldoPos = v.SaldoLiquidoCents >= 0;
        float xSaldo = Margin + colWs[0] + colWs[1];
        canvas.DrawRect(new SKRect(xSaldo, y, xSaldo + colWs[2], y + boxH),
            saldoPos ? paintFillGreenTint : paintFillRedTint);

        float xMissing = Margin + colWs[0] + colWs[1] + colWs[2];
        bool hasMissing = ctx.Audit is not null && v.XmlComprasOmissas > 0;
        if (ctx.Audit is not null)
        {
            canvas.DrawRect(new SKRect(xMissing, y, Margin + ContentW, y + boxH),
                hasMissing ? paintFillRedTint : paintFillGreenTint);
        }
        canvas.DrawRoundRect(boxRect, 4f, 4f, paintLine);

        float cx = Margin;
        for (int i = 0; i < 3; i++)
        {
            cx += colWs[i];
            canvas.DrawLine(cx, y, cx, y + boxH, paintLine);
        }

        DrawSummaryBoxCell(canvas, Margin, colWs[0], y, "Total de Entradas",
            $"R$ {TaxIdValidator.FormatCentsBrl(v.TotalEntradasCents)}", tfReg, tfBold, paintMuted, paintDark);
        DrawSummaryBoxCell(canvas, Margin + colWs[0], colWs[1], y, "Total de Saídas (Vendas)",
            $"R$ {TaxIdValidator.FormatCentsBrl(v.TotalSaidasCents)}", tfReg, tfBold, paintMuted, paintDark);
        DrawSummaryBoxCell(canvas, xSaldo, colWs[2], y, "Saldo (Saídas - Entradas)",
            TaxIdValidator.FormatSignedCentsBrl(v.SaldoLiquidoCents),
            tfReg, tfBold, paintMuted, saldoPos ? paintGreen : paintRed);

        string missingText = ctx.Audit is null
            ? "XML não carregado"
            : (hasMissing ? $"{v.XmlComprasOmissas} ({($"R$ {TaxIdValidator.FormatCentsBrl(v.XmlComprasOmissasCents)}")})" : "Todas lançadas");
        DrawSummaryBoxCell(canvas, xMissing, colWs[3], y, "Compras Faltantes (XML)",
            missingText, tfReg, tfBold, paintMuted, ctx.Audit is null ? paintMuted : (hasMissing ? paintRed : paintGreen));

        y += boxH + 18f;

        // 3. Monthly Movement (for Completo mode)
        if (ctx.Mode == ReportMode.Completo && v.MonthlyRows.Count > 0)
        {
            EnsureSpace(70f);
            DrawSectionBand(canvas!, ref y, "Movimentação Mês a Mês (Entradas x Saídas x Saldo x Conferência XML)", tfBold, paintDark, paintFillHeader);
            y += 6f;

            float[] mCols = [46f, 70f, 60f, 74f, 74f, 82f, ContentW - 406f];
            DrawMonthlyHeaderRow(canvas!, y, mCols, tfBold, paintDark, paintFillHeader);
            y += 22f;

            bool alt = false;
            foreach (var m in v.MonthlyRows)
            {
                EnsureSpace(22f);
                var rowRect = new SKRect(Margin, y, Margin + ContentW, y + 20f);
                if (alt) canvas!.DrawRect(rowRect, paintFillAlt);
                canvas!.DrawLine(Margin, y + 20f, Margin + ContentW, y + 20f, paintLine);

                float mx = Margin;
                DrawCellLeft(canvas, mx, mCols[0], y + 13f, m.MonthLabel, tfReg, 8.2f, paintDark); mx += mCols[0];
                DrawCellRight(canvas, mx, mCols[1], y + 13f, $"R$ {TaxIdValidator.FormatCentsBrl(m.ComprasEntradasCents)}", tfReg, 8.0f, paintDark); mx += mCols[1];
                DrawCellRight(canvas, mx, mCols[2], y + 13f, $"R$ {TaxIdValidator.FormatCentsBrl(m.OutrasEntradasCents)}", tfReg, 8.0f, paintMuted); mx += mCols[2];
                DrawCellRight(canvas, mx, mCols[3], y + 13f, $"R$ {TaxIdValidator.FormatCentsBrl(m.TotalEntradasCents)}", tfBold, 8.0f, paintDark); mx += mCols[3];
                DrawCellRight(canvas, mx, mCols[4], y + 13f, $"R$ {TaxIdValidator.FormatCentsBrl(m.TotalSaidasCents)}", tfBold, 8.0f, paintDark); mx += mCols[4];
                DrawCellRight(canvas, mx, mCols[5], y + 13f, TaxIdValidator.FormatSignedCentsBrl(m.SaldoLiquidoCents), tfBold, 8.0f,
                    m.SaldoLiquidoCents >= 0 ? paintGreen : paintRed); mx += mCols[5];

                string xmlStatus = "Sem XML";
                SKPaint xmlPaint = paintMuted;
                if (ctx.Audit is not null)
                {
                    if (v.XmlCoverageByMonth.TryGetValue(m.YearMonth, out var cov) && cov.XmlComprasOmissas > 0)
                    {
                        xmlStatus = $"{cov.XmlComprasOmissas} falta (R$ {TaxIdValidator.FormatCentsBrl(cov.XmlComprasOmissasCents)})";
                        xmlPaint = paintRed;
                    }
                    else
                    {
                        xmlStatus = "Conferido (OK)";
                        xmlPaint = paintGreen;
                    }
                }
                DrawCellRight(canvas, mx, mCols[6], y + 13f, xmlStatus, tfBold, 8.0f, xmlPaint);

                y += 20f;
                alt = !alt;
            }

            // Total row
            EnsureSpace(24f);
            canvas!.DrawRect(new SKRect(Margin, y, Margin + ContentW, y + 22f), paintFillHeader);
            float tx = Margin;
            DrawCellLeft(canvas, tx, mCols[0], y + 14f, "Total", tfBold, 8.4f, paintDark); tx += mCols[0];
            DrawCellRight(canvas, tx, mCols[1], y + 14f, $"R$ {TaxIdValidator.FormatCentsBrl(v.ComprasEntradasCents)}", tfBold, 8.0f, paintDark); tx += mCols[1];
            DrawCellRight(canvas, tx, mCols[2], y + 14f, $"R$ {TaxIdValidator.FormatCentsBrl(v.OutrasEntradasCents)}", tfBold, 8.0f, paintDark); tx += mCols[2];
            DrawCellRight(canvas, tx, mCols[3], y + 14f, $"R$ {TaxIdValidator.FormatCentsBrl(v.TotalEntradasCents)}", tfBold, 8.0f, paintDark); tx += mCols[3];
            DrawCellRight(canvas, tx, mCols[4], y + 14f, $"R$ {TaxIdValidator.FormatCentsBrl(v.TotalSaidasCents)}", tfBold, 8.0f, paintDark); tx += mCols[4];
            DrawCellRight(canvas, tx, mCols[5], y + 14f, TaxIdValidator.FormatSignedCentsBrl(v.SaldoLiquidoCents), tfBold, 8.0f,
                saldoPos ? paintGreen : paintRed); tx += mCols[5];
            DrawCellRight(canvas, tx, mCols[6], y + 14f,
                ctx.Audit is null ? "-" : $"{v.XmlComprasOmissas} falta(s)", tfBold, 8.0f, hasMissing ? paintRed : paintGreen);
            y += 32f;
        }

        // 4. Omitted Purchase Invoices Table (Completo + SomenteNotasNaoLancadas)
        if (ctx.Mode is ReportMode.Completo or ReportMode.SomenteNotasNaoLancadas)
        {
            EnsureSpace(70f);
            DrawSectionBand(canvas!, ref y, "Notas Fiscais de Compra Não Lançadas no Sintegra", tfBold, paintDark, paintFillHeader);
            y += 8f;

            var missingRows = v.XmlDocumentRows.Where(r => r.IsCriticalGap).ToList();
            if (ctx.Audit is null)
            {
                DrawText(canvas!, "Nenhum arquivo XML foi importado para conferir notas não lançadas neste relatório.", Margin + 4f, y + 6f, tfReg, 9f, paintMuted);
                y += 22f;
            }
            else if (missingRows.Count == 0)
            {
                DrawText(canvas!, "Todas as notas fiscais de compra identificadas nos XMLs do período estão devidamente lançadas no Sintegra.", Margin + 4f, y + 6f, tfBold, 9f, paintGreen);
                y += 22f;
            }
            else
            {
                float[] oCols = [56f, 64f, 70f, ContentW - 275f, 85f];
                canvas!.DrawRect(new SKRect(Margin, y, Margin + ContentW, y + 20f), paintFillHeader);
                float ox = Margin;
                DrawCellLeft(canvas, ox, oCols[0], y + 13f, "Mês", tfBold, 8.3f, paintDark); ox += oCols[0];
                DrawCellLeft(canvas, ox, oCols[1], y + 13f, "Emissão", tfBold, 8.3f, paintDark); ox += oCols[1];
                DrawCellLeft(canvas, ox, oCols[2], y + 13f, "Nota / Série", tfBold, 8.3f, paintDark); ox += oCols[2];
                DrawCellLeft(canvas, ox, oCols[3], y + 13f, "Fornecedor (Emitente)", tfBold, 8.3f, paintDark); ox += oCols[3];
                DrawCellRight(canvas, ox, oCols[4], y + 13f, "Valor NF", tfBold, 8.3f, paintDark);
                y += 20f;

                bool alt = false;
                foreach (var miss in missingRows)
                {
                    EnsureSpace(20f);
                    if (alt) canvas!.DrawRect(new SKRect(Margin, y, Margin + ContentW, y + 19f), paintFillAlt);
                    canvas!.DrawLine(Margin, y + 19f, Margin + ContentW, y + 19f, paintLine);

                    ox = Margin;
                    DrawCellLeft(canvas, ox, oCols[0], y + 13f, TaxIdValidator.FormatMonthLabel(miss.YearMonth), tfReg, 8.2f, paintDark); ox += oCols[0];
                    DrawCellLeft(canvas, ox, oCols[1], y + 13f, miss.DataBr, tfReg, 8.2f, paintDark); ox += oCols[1];
                    DrawCellLeft(canvas, ox, oCols[2], y + 13f, $"{miss.Numero}/{miss.Serie}", tfBold, 8.2f, paintRed); ox += oCols[2];
                    DrawCellLeft(canvas, ox, oCols[3], y + 13f, TrimToLen(miss.EmitNome, 42), tfReg, 8.2f, paintDark); ox += oCols[3];
                    DrawCellRight(canvas, ox, oCols[4], y + 13f, $"R$ {TaxIdValidator.FormatCentsBrl(miss.XmlValorCents)}", tfBold, 8.2f, paintRed);

                    y += 19f;
                    alt = !alt;
                }

                EnsureSpace(22f);
                canvas!.DrawRect(new SKRect(Margin, y, Margin + ContentW, y + 21f), paintFillRedTint);
                DrawCellLeft(canvas, Margin, ContentW - 95f, y + 14f, $"Total não lançado ({missingRows.Count} nota(s)):", tfBold, 8.6f, paintRed);
                DrawCellRight(canvas, Margin + ContentW - 95f, 95f, y + 14f, $"R$ {TaxIdValidator.FormatCentsBrl(v.XmlComprasOmissasCents)}", tfBold, 8.6f, paintRed);
                y += 30f;
            }
        }

        // 5. Top Selling Products (when requested or in Completo when available)
        if ((ctx.Mode == ReportMode.ProdutosMaisVendidos || (ctx.Mode == ReportMode.Completo && v.TopSellingProducts.Count > 0)) &&
            v.TopSellingProducts.Count > 0)
        {
            int limit = ctx.Mode == ReportMode.ProdutosMaisVendidos ? 40 : 15;
            var top = v.TopSellingProducts.Take(limit).ToList();
            EnsureSpace(70f);
            DrawSectionBand(canvas!, ref y, $"Produtos Mais Vendidos / Movimentados (Top {top.Count})", tfBold, paintDark, paintFillHeader);
            y += 6f;

            float[] pCols = [34f, 68f, ContentW - 282f, 36f, 58f, 86f];
            canvas!.DrawRect(new SKRect(Margin, y, Margin + ContentW, y + 20f), paintFillHeader);
            float px = Margin;
            DrawCellLeft(canvas, px, pCols[0], y + 13f, "#", tfBold, 8.3f, paintDark); px += pCols[0];
            DrawCellLeft(canvas, px, pCols[1], y + 13f, "Código", tfBold, 8.3f, paintDark); px += pCols[1];
            DrawCellLeft(canvas, px, pCols[2], y + 13f, "Descrição do Produto", tfBold, 8.3f, paintDark); px += pCols[2];
            DrawCellLeft(canvas, px, pCols[3], y + 13f, "Un.", tfBold, 8.3f, paintDark); px += pCols[3];
            DrawCellRight(canvas, px, pCols[4], y + 13f, "Qtd.", tfBold, 8.3f, paintDark); px += pCols[4];
            DrawCellRight(canvas, px, pCols[5], y + 13f, "Total Vendido", tfBold, 8.3f, paintDark);
            y += 20f;

            int rank = 1;
            bool alt = false;
            foreach (var prod in top)
            {
                EnsureSpace(20f);
                if (alt) canvas!.DrawRect(new SKRect(Margin, y, Margin + ContentW, y + 19f), paintFillAlt);
                canvas!.DrawLine(Margin, y + 19f, Margin + ContentW, y + 19f, paintLine);

                px = Margin;
                DrawCellLeft(canvas, px, pCols[0], y + 13f, $"{rank++}.", tfReg, 8.2f, paintMuted); px += pCols[0];
                DrawCellLeft(canvas, px, pCols[1], y + 13f, TrimToLen(prod.CodProduto, 14), tfReg, 8.2f, paintDark); px += pCols[1];
                DrawCellLeft(canvas, px, pCols[2], y + 13f, TrimToLen(prod.Descricao, 38), tfReg, 8.2f, paintDark); px += pCols[2];
                DrawCellLeft(canvas, px, pCols[3], y + 13f, prod.Unidade, tfReg, 8.2f, paintMuted); px += pCols[3];
                DrawCellRight(canvas, px, pCols[4], y + 13f, TaxIdValidator.FormatMilliBr(prod.QuantidadeMilli), tfReg, 8.2f, paintDark); px += pCols[4];
                DrawCellRight(canvas, px, pCols[5], y + 13f, $"R$ {TaxIdValidator.FormatCentsBrl(prod.ValorTotalCents)}", tfBold, 8.2f, paintDark);
                y += 19f;
                alt = !alt;
            }
            y += 12f;
        }

        DrawFooter(canvas!, tfReg, paintMuted, paintLine, ctx.AppVersion, timestamp, pageNum);
        document.EndPage();
        document.Close();
    }

    private static string TrimToLen(string s, int max) =>
        s.Length <= max ? s : s[..Math.Max(0, max - 3)] + "...";

    private static SKTypeface ResolveTypeface(bool bold)
    {
        var style = bold ? SKFontStyle.Bold : SKFontStyle.Normal;
        foreach (string fam in new[] { "Segoe UI", "Inter", "Arial", "Liberation Sans", "DejaVu Sans" })
        {
            var tf = SKTypeface.FromFamilyName(fam, style);
            if (tf is not null) return tf;
        }
        return SKTypeface.Default;
    }

    private static void DrawHeader(
        SKCanvas c,
        SKTypeface tfReg,
        SKTypeface tfBold,
        SKPaint paintDark,
        SKPaint paintMuted,
        SKPaint heavyLine,
        string reportTitle,
        string periodLabel)
    {
        // OpenSintegra vector geometric emblem matching assets/brand/opensintegra-mark.svg
        const float s = 0.30f;
        static float Tx(float x) => Margin + x * s;
        static float Ty(float y) => Margin - 1f + y * s;

        using var iconPaint = new SKPaint
        {
            Color = SKColor.Parse("#0066FC"),
            IsStroke = true,
            StrokeWidth = 8.5f * s,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };
        using var p = new SKPath();
        p.MoveTo(Tx(50f), Ty(16.6f));
        p.LineTo(Tx(82.2f), Ty(36.1f));
        p.LineTo(Tx(50f), Ty(55.6f));
        p.LineTo(Tx(17.8f), Ty(36.1f));
        p.Close();

        p.MoveTo(Tx(17.8f), Ty(50.0f));
        p.LineTo(Tx(29.2f), Ty(56.9f));

        p.MoveTo(Tx(70.8f), Ty(56.9f));
        p.LineTo(Tx(82.2f), Ty(50.0f));

        p.MoveTo(Tx(17.8f), Ty(63.9f));
        p.LineTo(Tx(50f), Ty(83.4f));
        p.LineTo(Tx(82.2f), Ty(63.9f));
        c.DrawPath(p, iconPaint);

        DrawText(c, "OpenSintegra", Margin + 36f, Margin + 13f, tfBold, 14f, paintDark);
        DrawText(c, "Contábil", Margin + 36f, Margin + 27f, tfReg, 11f, paintMuted);

        DrawTextRight(c, reportTitle, PageW - Margin, Margin + 14f, tfBold, 13.5f, paintDark);
        DrawTextRight(c, $"Período: {periodLabel}", PageW - Margin, Margin + 29f, tfReg, 10f, paintMuted);

        c.DrawLine(Margin, Margin + 36f, PageW - Margin, Margin + 36f, heavyLine);
    }

    private static void DrawSectionBand(SKCanvas c, ref float y, string title, SKTypeface tfBold, SKPaint dark, SKPaint fill)
    {
        c.DrawRoundRect(new SKRect(Margin, y, Margin + ContentW, y + 20f), 3f, 3f, fill);
        DrawText(c, title, Margin + 8f, y + 13.5f, tfBold, 10f, dark);
        y += 20f;
    }

    private static void DrawSummaryBoxCell(
        SKCanvas c, float x, float w, float y, string label, string value,
        SKTypeface tfReg, SKTypeface tfBold, SKPaint muted, SKPaint valPaint)
    {
        DrawText(c, label, x + 8f, y + 15f, tfReg, 8.2f, muted);
        DrawText(c, value, x + 8f, y + 34f, tfBold, 10.8f, valPaint);
    }

    private static void DrawMonthlyHeaderRow(SKCanvas c, float y, float[] cols, SKTypeface tfBold, SKPaint dark, SKPaint fill)
    {
        c.DrawRect(new SKRect(Margin, y, Margin + ContentW, y + 21f), fill);
        float x = Margin;
        DrawCellLeft(c, x, cols[0], y + 14f, "Mês", tfBold, 8.1f, dark); x += cols[0];
        DrawCellRight(c, x, cols[1], y + 14f, "Compras", tfBold, 8.1f, dark); x += cols[1];
        DrawCellRight(c, x, cols[2], y + 14f, "Outras Entr.", tfBold, 8.1f, dark); x += cols[2];
        DrawCellRight(c, x, cols[3], y + 14f, "Tot. Entr.", tfBold, 8.1f, dark); x += cols[3];
        DrawCellRight(c, x, cols[4], y + 14f, "Tot. Saídas", tfBold, 8.1f, dark); x += cols[4];
        DrawCellRight(c, x, cols[5], y + 14f, "Saídas - Entr.", tfBold, 8.1f, dark); x += cols[5];
        DrawCellRight(c, x, cols[6], y + 14f, "Conferência XML", tfBold, 8.1f, dark);
    }

    private static void DrawFooter(
        SKCanvas c, SKTypeface tfReg, SKPaint muted, SKPaint line, string version, string timestamp, int pageNum)
    {
        float fy = PageH - Margin;
        c.DrawLine(Margin, fy - 14f, PageW - Margin, fy - 14f, line);
        DrawText(c, $"OpenSintegra Contábil v{version}", Margin, fy - 2f, tfReg, 8.5f, muted);
        DrawTextRight(c, $"Relatório gerado em {timestamp}  |  Página {pageNum}", PageW - Margin, fy - 2f, tfReg, 8.5f, muted);
    }

    private static void DrawText(SKCanvas c, string text, float x, float baselineY, SKTypeface tf, float size, SKPaint paint)
    {
        using var font = new SKFont(tf, size);
        c.DrawText(text, x, baselineY, font, paint);
    }

    private static void DrawTextRight(SKCanvas c, string text, float rightX, float baselineY, SKTypeface tf, float size, SKPaint paint)
    {
        using var font = new SKFont(tf, size);
        using var measurePaint = new SKPaint { Typeface = tf, TextSize = size, IsAntialias = true };
        float w = measurePaint.MeasureText(text);
        c.DrawText(text, rightX - w, baselineY, font, paint);
    }

    private static void DrawCellLeft(SKCanvas c, float x, float w, float y, string text, SKTypeface tf, float size, SKPaint paint)
        => DrawText(c, text, x + 6f, y, tf, size, paint);

    private static void DrawCellRight(SKCanvas c, float x, float w, float y, string text, SKTypeface tf, float size, SKPaint paint)
        => DrawTextRight(c, text, x + w - 6f, y, tf, size, paint);
}
