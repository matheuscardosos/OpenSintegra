using ClosedXML.Excel;
using OpenSintegra.Core;

namespace OpenSintegra.Reports;

/// <summary>
/// Generates formatted multi-sheet Excel (.xlsx) accounting workbooks using ClosedXML (MIT license).
/// </summary>
public static class ExcelReportGenerator
{
    private const string BrlFormat = "R$ #,##0.00";

    public static void GenerateWorkbook(FiscalReportContext ctx, string outputPath)
    {
        using var wb = new XLWorkbook();
        wb.Properties.Title = "Relatório Fiscal OpenSintegra";
        wb.Properties.Company = ctx.Document.Reg10?.NomeContribuinte ?? "OpenSintegra";

        BuildExecutiveSheet(wb, ctx);
        BuildMonthlySheet(wb, ctx);
        if (ctx.ScopedView.XmlDocumentRows.Count > 0)
            BuildXmlAuditSheet(wb, ctx);
        if (ctx.ScopedView.TopSellingProducts.Count > 0)
            BuildTopProductsSheet(wb, ctx);
        BuildCfopSheet(wb, ctx);
        BuildInvoicesSheet(wb, ctx);
        if (ctx.ScopedView.Reg61Docs.Count > 0)
            BuildReg61Sheet(wb, ctx);
        BuildDiagnosticsSheet(wb, ctx);

        wb.SaveAs(outputPath);
    }

    private static void BuildExecutiveSheet(XLWorkbook wb, FiscalReportContext ctx)
    {
        var ws = wb.Worksheets.Add("Resumo Executivo");
        var r10 = ctx.Document.Reg10;
        var v = ctx.ScopedView;

        ws.Cell(1, 1).Value = "OpenSintegra Contábil - Relatório Fiscal de Entradas e Saídas";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 15;
        ws.Cell(2, 1).Value = $"Período analisado: {v.PeriodLabel}";
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#475569");

        int row = 4;
        void AddKv(string label, object val, string? numFmt = null, bool boldVal = false)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            var cell = ws.Cell(row, 2);
            cell.Value = XLCellValue.FromObject(val);
            if (!string.IsNullOrEmpty(numFmt)) cell.Style.NumberFormat.Format = numFmt;
            if (boldVal) cell.Style.Font.Bold = true;
            row++;
        }

        AddKv("Razão Social", r10?.NomeContribuinte ?? "-");
        AddKv("CNPJ", r10 is null ? "-" : TaxIdValidator.FormatCnpj(r10.Cnpj));
        AddKv("Inscrição Estadual", r10?.Ie ?? "-");
        AddKv("UF", r10?.Uf ?? "-");
        AddKv("Regime Tributário", ctx.TaxRegime);
        AddKv("Convênio", "ICMS 57/95 (Sintegra)");

        row++;
        AddKv("Compras Comerciais Lançadas", v.ComprasEntradasCents / 100m, BrlFormat);
        AddKv("Outras Entradas (Devolução / Bonif. / etc.)", v.OutrasEntradasCents / 100m, BrlFormat);
        AddKv("Total de Entradas no Sintegra", v.TotalEntradasCents / 100m, BrlFormat, true);
        AddKv("Total de Saídas / Vendas no Sintegra", v.TotalSaidasCents / 100m, BrlFormat, true);
        AddKv("Saldo (Saídas - Entradas)", v.SaldoLiquidoCents / 100m, BrlFormat, true);

        if (ctx.Audit is not null)
        {
            row++;
            AddKv("Notas de Compra nos XMLs", v.XmlComprasTotal);
            AddKv("Notas de Compra Lançadas no Sintegra", v.XmlComprasLancadas);
            AddKv("Notas de Compra Não Lançadas (Faltantes)", v.XmlComprasOmissas, boldVal: v.XmlComprasOmissas > 0);
            AddKv("Valor Total Não Lançado", v.XmlComprasOmissasCents / 100m, BrlFormat, v.XmlComprasOmissas > 0);
        }

        ws.Columns().AdjustToContents(1, 30, 60);
    }

    private static void BuildMonthlySheet(XLWorkbook wb, FiscalReportContext ctx)
    {
        var ws = wb.Worksheets.Add("Movimentação Mensal");
        string[] headers =
        [
            "Mês", "Compras", "Outras Entradas", "Total Entradas",
            "Saídas Reg 50", "Saídas NFC-e (61)", "Total Saídas", "Saldo (Saídas - Entradas)", "Notas XML Faltantes", "Valor Não Lançado"
        ];
        WriteTableHeader(ws, headers);

        int r = 2;
        foreach (var m in ctx.ScopedView.MonthlyRows)
        {
            ctx.ScopedView.XmlCoverageByMonth.TryGetValue(m.YearMonth, out var cov);
            int missing = cov?.XmlComprasOmissas ?? 0;
            long missingVal = cov?.XmlComprasOmissasCents ?? 0;

            ws.Cell(r, 1).Value = m.MonthLabel;
            ws.Cell(r, 2).Value = m.ComprasEntradasCents / 100m;
            ws.Cell(r, 3).Value = m.OutrasEntradasCents / 100m;
            ws.Cell(r, 4).Value = m.TotalEntradasCents / 100m;
            ws.Cell(r, 5).Value = m.SaidasReg50Cents / 100m;
            ws.Cell(r, 6).Value = m.SaidasReg61Cents / 100m;
            ws.Cell(r, 7).Value = m.TotalSaidasCents / 100m;
            ws.Cell(r, 8).FormulaA1 = $"=G{r}-D{r}";
            ws.Cell(r, 9).Value = missing;
            ws.Cell(r, 10).Value = missingVal / 100m;

            for (int c = 2; c <= 8; c++)
                ws.Cell(r, c).Style.NumberFormat.Format = BrlFormat;
            ws.Cell(r, 8).Style.Font.Bold = true;
            ws.Cell(r, 8).Style.Font.FontColor = m.SaldoLiquidoCents >= 0 ? XLColor.FromHtml("#15803D") : XLColor.FromHtml("#B91C1C");
            ws.Cell(r, 10).Style.NumberFormat.Format = BrlFormat;
            if (missing > 0)
            {
                ws.Cell(r, 9).Style.Font.FontColor = XLColor.FromHtml("#B91C1C");
                ws.Cell(r, 9).Style.Font.Bold = true;
                ws.Cell(r, 10).Style.Font.FontColor = XLColor.FromHtml("#B91C1C");
            }
            r++;
        }

        if (r > 2)
        {
            ws.Cell(r, 1).Value = "TOTAL";
            for (int c = 2; c <= 8; c++)
            {
                string col = XLHelper.GetColumnLetterFromNumber(c);
                ws.Cell(r, c).FormulaA1 = $"=SUM({col}2:{col}{r - 1})";
                ws.Cell(r, c).Style.NumberFormat.Format = BrlFormat;
            }
            ws.Cell(r, 9).FormulaA1 = $"=SUM(I2:I{r - 1})";
            ws.Cell(r, 10).FormulaA1 = $"=SUM(J2:J{r - 1})";
            ws.Cell(r, 10).Style.NumberFormat.Format = BrlFormat;
            ws.Row(r).Style.Font.Bold = true;
            ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");
        }

        ws.Columns().AdjustToContents(1, 14, 30);
    }

    private static void BuildTopProductsSheet(XLWorkbook wb, FiscalReportContext ctx)
    {
        var ws = wb.Worksheets.Add("Produtos Mais Vendidos");
        WriteTableHeader(ws, ["Posição", "Código Produto", "Descrição", "Unidade", "Qtd. Vendida", "Total Vendido", "Participação %"]);

        int r = 2;
        foreach (var p in ctx.ScopedView.TopSellingProducts)
        {
            ws.Cell(r, 1).Value = r - 1;
            ws.Cell(r, 2).Value = p.CodProduto;
            ws.Cell(r, 3).Value = p.Descricao;
            ws.Cell(r, 4).Value = p.Unidade;
            ws.Cell(r, 5).Value = p.QuantidadeMilli / 1000m;
            ws.Cell(r, 6).Value = p.ValorTotalCents / 100m;
            ws.Cell(r, 7).Value = p.SharePct / 100.0;
            ws.Cell(r, 5).Style.NumberFormat.Format = "#,##0.###";
            ws.Cell(r, 6).Style.NumberFormat.Format = BrlFormat;
            ws.Cell(r, 7).Style.NumberFormat.Format = "0.00%";
            r++;
        }
        ws.Columns().AdjustToContents(1, 14, 48);
    }

    private static void BuildCfopSheet(XLWorkbook wb, FiscalReportContext ctx)
    {
        var ws = wb.Worksheets.Add("Apuração por CFOP");
        WriteTableHeader(ws, ["CFOP", "Natureza da Operação", "Notas", "Valor Contábil", "Base ICMS", "Valor ICMS", "Isenta", "Outras"]);

        int r = 2;
        foreach (var c in ctx.ScopedView.CfopRows)
        {
            ws.Cell(r, 1).Value = c.Cfop;
            ws.Cell(r, 2).Value = c.Nature;
            ws.Cell(r, 3).Value = c.DocumentCount;
            ws.Cell(r, 4).Value = c.ValorContabilCents / 100m;
            ws.Cell(r, 5).Value = c.BaseIcmsCents / 100m;
            ws.Cell(r, 6).Value = c.ValorIcmsCents / 100m;
            ws.Cell(r, 7).Value = c.IsentaCents / 100m;
            ws.Cell(r, 8).Value = c.OutrasCents / 100m;
            for (int col = 4; col <= 8; col++)
                ws.Cell(r, col).Style.NumberFormat.Format = BrlFormat;
            r++;
        }
        ws.Columns().AdjustToContents(1, 12, 42);
    }

    private static void BuildInvoicesSheet(XLWorkbook wb, FiscalReportContext ctx)
    {
        var ws = wb.Worksheets.Add("Notas Reg 50");
        WriteTableHeader(ws, ["Data", "Número", "Série", "Modelo", "CNPJ Emit./Dest.", "UF", "CFOP", "Emitente", "Valor Total", "Base ICMS", "Valor ICMS", "Situação"]);

        int r = 2;
        foreach (var nf in ctx.ScopedView.Reg50Docs)
        {
            ws.Cell(r, 1).Value = TaxIdValidator.FormatDateBr(nf.DataEmissao);
            ws.Cell(r, 2).Value = nf.Numero.Trim();
            ws.Cell(r, 3).Value = nf.Serie;
            ws.Cell(r, 4).Value = nf.Modelo;
            ws.Cell(r, 5).Value = TaxIdValidator.FormatCnpj(nf.Cnpj);
            ws.Cell(r, 6).Value = nf.Uf;
            ws.Cell(r, 7).Value = nf.Cfop;
            ws.Cell(r, 8).Value = nf.Emitente;
            ws.Cell(r, 9).Value = nf.ValorTotalCents / 100m;
            ws.Cell(r, 10).Value = nf.BaseIcmsCents / 100m;
            ws.Cell(r, 11).Value = nf.ValorIcmsCents / 100m;
            ws.Cell(r, 12).Value = nf.Situacao;
            for (int col = 9; col <= 11; col++)
                ws.Cell(r, col).Style.NumberFormat.Format = BrlFormat;
            r++;
        }
        ws.Columns().AdjustToContents(1, 12, 32);
    }

    private static void BuildReg61Sheet(XLWorkbook wb, FiscalReportContext ctx)
    {
        var ws = wb.Worksheets.Add("Saídas NFC-e Reg 61");
        WriteTableHeader(ws, ["Data", "Modelo", "Ordem Inicial", "Ordem Final", "Valor Total", "Base ICMS", "Valor ICMS"]);

        int r = 2;
        foreach (var r61 in ctx.ScopedView.Reg61Docs)
        {
            ws.Cell(r, 1).Value = TaxIdValidator.FormatDateBr(r61.DataEmissao);
            ws.Cell(r, 2).Value = r61.Modelo;
            ws.Cell(r, 3).Value = r61.NumInicialOrdem.TrimStart('0');
            ws.Cell(r, 4).Value = r61.NumFinalOrdem.TrimStart('0');
            ws.Cell(r, 5).Value = r61.ValorTotalCents / 100m;
            ws.Cell(r, 6).Value = r61.BaseIcmsCents / 100m;
            ws.Cell(r, 7).Value = r61.ValorIcmsCents / 100m;
            for (int col = 5; col <= 7; col++)
                ws.Cell(r, col).Style.NumberFormat.Format = BrlFormat;
            r++;
        }
        ws.Columns().AdjustToContents(1, 12, 30);
    }

    private static void BuildXmlAuditSheet(XLWorkbook wb, FiscalReportContext ctx)
    {
        var ws = wb.Worksheets.Add("Confronto XML");
        WriteTableHeader(ws, ["Mês", "Emissão", "Fornecedor", "CNPJ", "Número", "Classificação", "Valor XML", "Valor Sintegra", "Status", "CFOP XML", "CFOP Sintegra", "Observações"]);

        int r = 2;
        foreach (var d in ctx.ScopedView.XmlDocumentRows)
        {
            ws.Cell(r, 1).Value = TaxIdValidator.FormatMonthLabel(d.YearMonth);
            ws.Cell(r, 2).Value = d.DataBr;
            ws.Cell(r, 3).Value = d.EmitNome;
            ws.Cell(r, 4).Value = TaxIdValidator.FormatCnpj(d.EmitCnpj);
            ws.Cell(r, 5).Value = d.Numero;
            ws.Cell(r, 6).Value = d.CategoryLabel;
            ws.Cell(r, 7).Value = d.XmlValorCents / 100m;
            ws.Cell(r, 8).Value = d.SintegraValorCents / 100m;
            ws.Cell(r, 9).Value = d.MatchStatus;
            ws.Cell(r, 10).Value = d.XmlCfops;
            ws.Cell(r, 11).Value = d.SintegraCfops;
            ws.Cell(r, 12).Value = d.Notes;
            ws.Cell(r, 7).Style.NumberFormat.Format = BrlFormat;
            ws.Cell(r, 8).Style.NumberFormat.Format = BrlFormat;
            if (d.IsCriticalGap)
            {
                ws.Cell(r, 9).Style.Font.FontColor = XLColor.FromHtml("#B91C1C");
                ws.Cell(r, 9).Style.Font.Bold = true;
            }
            r++;
        }
        ws.Columns().AdjustToContents(1, 12, 48);
    }

    private static void BuildDiagnosticsSheet(XLWorkbook wb, FiscalReportContext ctx)
    {
        var ws = wb.Worksheets.Add("Diagnóstico");
        WriteTableHeader(ws, ["Código", "Severidade", "Linha", "Registro", "Campo", "Mensagem", "Encontrado", "Esperado"]);

        int r = 2;
        foreach (var d in ctx.ScopedView.Diagnostics)
        {
            ws.Cell(r, 1).Value = d.Code;
            ws.Cell(r, 2).Value = d.SeverityLabel;
            ws.Cell(r, 3).Value = d.Line;
            ws.Cell(r, 4).Value = d.RecordType;
            ws.Cell(r, 5).Value = d.FieldName;
            ws.Cell(r, 6).Value = d.Message;
            ws.Cell(r, 7).Value = d.ActualValue;
            ws.Cell(r, 8).Value = d.ExpectedHint;
            r++;
        }
        ws.Columns().AdjustToContents(1, 12, 60);
    }

    private static void WriteTableHeader(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.SheetView.FreezeRows(1);
    }
}
