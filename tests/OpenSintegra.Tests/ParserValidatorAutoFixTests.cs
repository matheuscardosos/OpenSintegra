using System;
using System.Linq;
using System.Text;
using System.Threading;
using OpenSintegra.Core;
using Xunit;

namespace OpenSintegra.Tests;

public sealed class ParserValidatorAutoFixTests
{
    private static string BuildSampleAnnualWithErrors()
    {
        var sb = new StringBuilder();
        // Deliberate LF-only endings + out-of-order item 54 + orphan Reg 75 + missing PROD54_B + Reg 61R PROD61 + wrong Reg 90
        sb.Append(SintegraFixtureBuilder.Reg10("11222333000181", "ISENTO", "EMPRESA MODELO SINTEGRA LTDA", "20260101", "20260228")).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg11()).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg50("00000000000191", "20260110", "000010", "1102", 1_000_000)).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg54("00000000000191", "000010", "1102", "002", "PROD54_B", 500_000)).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg54("00000000000191", "000010", "1102", "001", "PROD54_A", 500_000)).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg50("00000000000191", "20260215", "000020", "1102", 2_000_000)).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg54("00000000000191", "000020", "1102", "001", "PROD54_A", 2_000_000)).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg61("20260131", 1_500_000)).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg61("20260228", 2_500_000)).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg61R("012026", "PROD61", 1_500_000)).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg75("20260101", "20260228", "PROD54_A", "PRODUTO A")).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg75("20260101", "20260228", "PROD61", "PRODUTO VENDIDO NFC-E")).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg75("20260101", "20260228", "ORFAO_SEM_USO", "PRODUTO ORFAO")).Append('\n');
        sb.Append(SintegraFixtureBuilder.Reg90("11222333000181", "ISENTO", ("50", 1), ("99", 1))).Append('\n');
        return sb.ToString();
    }

    [Fact]
    public void Validator_DetectsMultiMonthAndCrlfAndReg90Errors()
    {
        byte[] bytes = Encoding.Latin1.GetBytes(BuildSampleAnnualWithErrors());
        var doc = SintegraParser.ParseBytes(bytes);
        var report = SintegraValidator.Validate(doc);

        Assert.True(report.Summary.IsMultiMonthPeriod);
        Assert.Equal(2, report.Summary.DistinctMonthsCount);
        Assert.Equal(2, report.MonthlyBalance.Count);
        Assert.Contains(report.Diagnostics, d => d.Code == "SIN-SYN-002");
        Assert.Contains(report.Diagnostics, d => d.Severity == Severity.Error);
    }

    [Fact]
    public void AutoFixer_FixesAllErrors_AndPreservesReg75ForReg61R()
    {
        byte[] bytes = Encoding.Latin1.GetBytes(BuildSampleAnnualWithErrors());
        var fx = SintegraAutoFixer.AutoFix(bytes, new FixOptions());

        Assert.True(fx.BeforeSummary.ErrorCount > 0);
        Assert.Equal(0, fx.AfterSummary.ErrorCount);
        Assert.True(fx.AfterSummary.Valid);

        var afterDoc = SintegraParser.ParseBytes(fx.FixedBytes);
        Assert.Contains(afterDoc.Reg75, p => p.CodProduto == "PROD61");
        Assert.Contains(afterDoc.Reg75, p => p.CodProduto == "PROD54_B");
        Assert.DoesNotContain(afterDoc.Reg75, p => p.CodProduto == "ORFAO_SEM_USO");
        Assert.DoesNotContain(afterDoc.Reg90.SelectMany(r => r.Counters), c => c.TipoATotalizar is "10" or "11");
    }

    [Fact]
    public void SplitAnnualByMonth_CreatesIndependentValidMonthlyFiles()
    {
        byte[] bytes = Encoding.Latin1.GetBytes(BuildSampleAnnualWithErrors());
        var splits = SintegraAutoFixer.SplitAnnualByMonth(bytes);

        Assert.Equal(2, splits.Count);
        Assert.Equal("SINTEGRA_202601.TXT", splits[0].SuggestedFileName);
        Assert.Equal("SINTEGRA_202602.TXT", splits[1].SuggestedFileName);
        Assert.All(splits, s =>
        {
            Assert.Equal(0, s.Summary.ErrorCount);
            Assert.False(s.Summary.IsMultiMonthPeriod);
        });
    }

    [Fact]
    public void PeriodFilter_ScopesKpisAndDocsToSingleMonth()
    {
        byte[] bytes = Encoding.Latin1.GetBytes(BuildSampleAnnualWithErrors());
        var fx = SintegraAutoFixer.AutoFix(bytes, new FixOptions());
        var doc = SintegraParser.ParseBytes(fx.FixedBytes);
        var rep = SintegraValidator.Validate(doc);

        var allView = PeriodFilter.CreateScopedView(doc, rep, null, "ALL");
        Assert.False(allView.IsSingleMonthFilter);
        Assert.Equal(3_000_000L, allView.TotalEntradasCents);
        Assert.Equal(4_000_000L, allView.TotalSaidasCents);

        var janView = PeriodFilter.CreateScopedView(doc, rep, null, "202601");
        Assert.True(janView.IsSingleMonthFilter);
        Assert.Equal(1_000_000L, janView.TotalEntradasCents);
        Assert.Equal(1_500_000L, janView.TotalSaidasCents);
        Assert.Single(janView.MonthlyRows);
        Assert.Single(janView.Reg50Docs);
    }

    [Fact]
    public void Parser_HonorsCancellationToken()
    {
        byte[] bytes = Encoding.Latin1.GetBytes(BuildSampleAnnualWithErrors());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => SintegraParser.ParseBytes(bytes, null, cts.Token));
    }
}
