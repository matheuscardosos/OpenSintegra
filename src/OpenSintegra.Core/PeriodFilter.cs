namespace OpenSintegra.Core;

public sealed record TopProductRow(
    string CodProduto,
    string Descricao,
    string Unidade,
    long QuantidadeMilli,
    long ValorTotalCents,
    double SharePct
);

/// <summary>
/// Represents a period-scoped view (either full period or a single YYYYMM month)
/// used consistently across UI dashboards, print previews, PDF and XLSX exports.
/// </summary>
public sealed record ScopedFiscalView(
    string? SelectedYearMonth,
    string PeriodLabel,
    bool IsSingleMonthFilter,
    long ComprasEntradasCents,
    long OutrasEntradasCents,
    long TotalEntradasCents,
    long SaidasReg50Cents,
    long SaidasReg61Cents,
    long SaidasReg60MCents,
    long TotalSaidasCents,
    long IcmsEntradasCents,
    long IcmsSaidasCents,
    long SaldoLiquidoCents,
    double? RatioSaidaSobreEntradaPct,
    double? EntradasPctSobreSaidas,
    IReadOnlyList<MonthlyBalanceRow> MonthlyRows,
    IReadOnlyList<CfopSummaryRow> CfopRows,
    IReadOnlyList<Reg50> Reg50Docs,
    IReadOnlyList<Reg61> Reg61Docs,
    IReadOnlyList<Diagnostic> Diagnostics,
    int XmlComprasTotal,
    int XmlComprasLancadas,
    int XmlComprasOmissas,
    long XmlComprasOmissasCents,
    IReadOnlyList<XmlAuditDocumentRow> XmlDocumentRows,
    IReadOnlyList<TopProductRow> TopSellingProducts,
    IReadOnlyDictionary<string, MonthlyXmlCoverageRow> XmlCoverageByMonth
);

public static class PeriodFilter
{
    public static ScopedFiscalView CreateScopedView(
        SintegraDocument doc,
        ValidationReport report,
        AuditReport? audit,
        string? yearMonthFilter)
    {
        bool isFiltered = !string.IsNullOrWhiteSpace(yearMonthFilter) && yearMonthFilter != "ALL";

        string periodLabel = isFiltered
            ? $"Competencia {TaxIdValidator.FormatMonthLabel(yearMonthFilter!)}"
            : (doc.Reg10 is { } r10
                ? $"{TaxIdValidator.FormatDateBr(r10.DataInicial)} a {TaxIdValidator.FormatDateBr(r10.DataFinal)}"
                : "Periodo completo");

        var reg50Scoped = isFiltered
            ? doc.Reg50.Where(r => r.DataEmissao.StartsWith(yearMonthFilter!, StringComparison.Ordinal)).ToList()
            : doc.Reg50;

        var reg61Scoped = isFiltered
            ? doc.Reg61.Where(r => r.DataEmissao.StartsWith(yearMonthFilter!, StringComparison.Ordinal)).ToList()
            : doc.Reg61;

        var reg60mScoped = isFiltered
            ? doc.Reg60M.Where(r => r.DataEmissao.StartsWith(yearMonthFilter!, StringComparison.Ordinal)).ToList()
            : doc.Reg60M;

        var reg70Scoped = isFiltered
            ? doc.Reg70.Where(r => r.DataEmissao.StartsWith(yearMonthFilter!, StringComparison.Ordinal)).ToList()
            : doc.Reg70;

        var monthlyRows = isFiltered
            ? report.MonthlyBalance.Where(m => m.YearMonth == yearMonthFilter).ToList()
            : report.MonthlyBalance.ToList();

        long compras = 0, outras = 0, sai50 = 0, sai61 = 0, sai60m = 0, icmsEnt = 0, icmsSai = 0;
        var cfopMap = new SortedDictionary<string, CfopSummaryRow>(StringComparer.Ordinal);

        CfopSummaryRow GetCfop(string cfop)
        {
            if (!cfopMap.TryGetValue(cfop, out var r))
            {
                r = new CfopSummaryRow { Cfop = cfop, Nature = TaxIdValidator.CfopNature(cfop) };
                cfopMap[cfop] = r;
            }
            return r;
        }

        foreach (var r50 in reg50Scoped)
        {
            if (r50.Situacao == "S") continue;
            var row = GetCfop(r50.Cfop);
            row.DocumentCount++;
            row.ValorContabilCents += r50.ValorTotalCents;
            row.BaseIcmsCents += r50.BaseIcmsCents;
            row.ValorIcmsCents += r50.ValorIcmsCents;
            row.IsentaCents += r50.IsentaNaoTribCents;
            row.OutrasCents += r50.OutrasCents;

            if (TaxIdValidator.IsEntradaCfop(r50.Cfop))
            {
                icmsEnt += r50.ValorIcmsCents;
                if (TaxIdValidator.IsCompraComercialCfop(r50.Cfop))
                    compras += r50.ValorTotalCents;
                else
                    outras += r50.ValorTotalCents;
            }
            else
            {
                sai50 += r50.ValorTotalCents;
                icmsSai += r50.ValorIcmsCents;
            }
        }

        foreach (var r61 in reg61Scoped)
        {
            sai61 += r61.ValorTotalCents;
            icmsSai += r61.ValorIcmsCents;
        }

        foreach (var r60m in reg60mScoped)
            sai60m += r60m.VendaBrutaCents;

        foreach (var r70 in reg70Scoped)
        {
            if (r70.Situacao == "S") continue;
            var row = GetCfop(r70.Cfop);
            row.DocumentCount++;
            row.ValorContabilCents += r70.ValorTotalCents;
            row.BaseIcmsCents += r70.BaseIcmsCents;
            row.ValorIcmsCents += r70.ValorIcmsCents;
            if (TaxIdValidator.IsEntradaCfop(r70.Cfop))
            {
                outras += r70.ValorTotalCents;
                icmsEnt += r70.ValorIcmsCents;
            }
            else
            {
                sai50 += r70.ValorTotalCents;
                icmsSai += r70.ValorIcmsCents;
            }
        }

        long totEnt = compras + outras;
        long totSai = sai50 + sai61 + sai60m;
        long saldo = totSai - totEnt;
        double? ratio = totEnt > 0 ? Math.Round(totSai * 100.0 / totEnt, 1) : null;
        double? entPct = totSai > 0 ? Math.Round(totEnt * 100.0 / totSai, 1) : null;

        // Filter line-based diagnostics if single month selected
        IReadOnlyList<Diagnostic> diags = report.Diagnostics;
        if (isFiltered)
        {
            var validLines = new HashSet<int>();
            foreach (var r in reg50Scoped) validLines.Add(r.Line);
            foreach (var r in reg61Scoped) validLines.Add(r.Line);
            foreach (var r in reg70Scoped) validLines.Add(r.Line);
            diags = report.Diagnostics
                .Where(d => d.RecordType is "10" or "11" or "90" || validLines.Contains(d.Line))
                .ToList();
        }

        IReadOnlyList<XmlAuditDocumentRow> xmlRows = Array.Empty<XmlAuditDocumentRow>();
        int xCompTot = 0, xCompLanc = 0, xCompOmiss = 0;
        long xCompOmissCents = 0;
        var xmlCovMap = new Dictionary<string, MonthlyXmlCoverageRow>(StringComparer.Ordinal);

        if (audit is not null)
        {
            xmlRows = isFiltered
                ? audit.DocumentRows.Where(r => r.YearMonth == yearMonthFilter).ToList()
                : audit.DocumentRows;

            var comp = xmlRows.Where(r => r.Category == XmlOperationCategory.CompraComercial).ToList();
            xCompTot = comp.Count;
            xCompLanc = comp.Count(r => r.MatchStatus == "Lancada OK");
            var omissas = comp.Where(r => r.MatchStatus == "OMISSA NO SINTEGRA").ToList();
            xCompOmiss = omissas.Count;
            xCompOmissCents = omissas.Sum(r => r.XmlValorCents);

            foreach (var mc in audit.MonthlyCoverage)
                xmlCovMap[mc.YearMonth] = mc;
        }

        // Top-selling products aggregated from Reg 61R (NFC-e / Cupom) + own-issuance Reg 54 sales
        var prodDescMap = new Dictionary<string, (string Desc, string Unid)>(StringComparer.Ordinal);
        foreach (var r75 in doc.Reg75)
            prodDescMap.TryAdd(r75.CodProduto, (r75.Descricao.Trim(), r75.Unidade.Trim()));

        string? filterMmYyyy = (isFiltered && yearMonthFilter!.Length == 6)
            ? yearMonthFilter[4..6] + yearMonthFilter[..4]
            : null;

        var topAgg = new Dictionary<string, (long QtyMilli, long ValCents)>(StringComparer.Ordinal);
        foreach (var r61r in doc.Reg61R)
        {
            if (filterMmYyyy is not null && r61r.MesAno != filterMmYyyy) continue;
            if (string.IsNullOrEmpty(r61r.CodProduto)) continue;
            topAgg.TryGetValue(r61r.CodProduto, out var cur);
            topAgg[r61r.CodProduto] = (cur.QtyMilli + r61r.QuantidadeMilli, cur.ValCents + r61r.ValorBrutoCents);
        }

        // Also include sales items from Reg 54 (exit CFOPs 5/6/7) for scoped Reg 50 invoices
        var exit50Keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r50 in reg50Scoped)
        {
            if (r50.Situacao != "S" && !TaxIdValidator.IsEntradaCfop(r50.Cfop))
                exit50Keys.Add($"{r50.Cnpj}|{r50.Modelo}|{r50.Serie}|{r50.Numero}");
        }
        foreach (var r54 in doc.Reg54)
        {
            if (r54.NumItem is "991" or "992" or "999" || string.IsNullOrEmpty(r54.CodProduto)) continue;
            if (exit50Keys.Contains($"{r54.Cnpj}|{r54.Modelo}|{r54.Serie}|{r54.Numero}"))
            {
                topAgg.TryGetValue(r54.CodProduto, out var cur);
                topAgg[r54.CodProduto] = (cur.QtyMilli + r54.QuantidadeMilli, cur.ValCents + r54.ValorProdutoCents);
            }
        }

        long allSoldCents = topAgg.Values.Sum(v => v.ValCents);
        var topProducts = topAgg
            .Select(kv =>
            {
                prodDescMap.TryGetValue(kv.Key, out var info);
                string desc = string.IsNullOrEmpty(info.Desc) ? $"PRODUTO {kv.Key}" : info.Desc;
                string unid = string.IsNullOrEmpty(info.Unid) ? "UN" : info.Unid;
                double share = allSoldCents > 0 ? Math.Round(kv.Value.ValCents * 100.0 / allSoldCents, 2) : 0;
                return new TopProductRow(kv.Key, desc, unid, kv.Value.QtyMilli, kv.Value.ValCents, share);
            })
            .OrderByDescending(p => p.ValorTotalCents)
            .ThenByDescending(p => p.QuantidadeMilli)
            .ToList();

        return new ScopedFiscalView(
            isFiltered ? yearMonthFilter : null,
            periodLabel,
            isFiltered,
            compras,
            outras,
            totEnt,
            sai50,
            sai61,
            sai60m,
            totSai,
            icmsEnt,
            icmsSai,
            saldo,
            ratio,
            entPct,
            monthlyRows,
            isFiltered ? cfopMap.Values.ToList() : report.CfopSummary,
            reg50Scoped,
            reg61Scoped,
            diags,
            xCompTot,
            xCompLanc,
            xCompOmiss,
            xCompOmissCents,
            xmlRows,
            topProducts,
            xmlCovMap);
    }
}
