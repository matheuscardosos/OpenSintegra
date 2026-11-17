using System;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using OpenSintegra.Core;
using OpenSintegra.Data;
using OpenSintegra.Reports;
using Xunit;

namespace OpenSintegra.Tests;

public sealed class DatabaseAndReportsTests : IDisposable
{
    private readonly string _tempDir;

    public DatabaseAndReportsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "OpenSintegraTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Ignore transient Windows SQLite lock during teardown
        }
    }

    private static byte[] MakeValidMonthBytes(string yyyymm, long entValCents, long saiValCents)
    {
        string di = yyyymm + "01";
        string df = yyyymm + "28";
        var sb = new StringBuilder();
        sb.Append(SintegraFixtureBuilder.Reg10("11222333000181", "ISENTO", "EMPRESA SQLITE RELATORIO LTDA", di, df)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg11()).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg50("00000000000191", yyyymm + "10", "000001", "1102", entValCents)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg61(df, saiValCents)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg90("11222333000181", "ISENTO", ("50", 1), ("61", 1), ("99", 5))).Append("\r\n");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    [Fact]
    public void FiscalDatabase_RecordsCompaniesTimelinesAndRespectsSettings()
    {
        string dbPath = Path.Combine(_tempDir, "test_fiscal.db");
        var db = new FiscalDatabase(dbPath);

        byte[] jan = MakeValidMonthBytes("202601", 2_000_000, 3_200_000);
        var janDoc = SintegraParser.ParseBytes(jan);
        var janRep = SintegraValidator.Validate(janDoc);
        Assert.Equal(0, janRep.Summary.ErrorCount);
        db.RecordImport(janDoc, janRep, null, "SINTEGRA_202601.TXT");

        byte[] feb = MakeValidMonthBytes("202602", 2_500_000, 3_600_000);
        var febDoc = SintegraParser.ParseBytes(feb);
        var febRep = SintegraValidator.Validate(febDoc);
        Assert.Equal(0, febRep.Summary.ErrorCount);
        db.RecordImport(febDoc, febRep, null, "SINTEGRA_202602.TXT");

        db.UpdateCompanyTaxRegime("11222333000181", "Lucro Presumido");
        db.UpdateSettings(2048, autoPrune: true);

        var companies = db.GetCompanies();
        Assert.Single(companies);
        Assert.Equal("Lucro Presumido", db.GetCompanyTaxRegime("11222333000181"));

        var timeline = db.GetCompanyMonthlyTimeline("11222333000181");
        Assert.Equal(2, timeline.Count);
        Assert.Equal("202601", timeline[0].YearMonth);
        Assert.Equal("202602", timeline[1].YearMonth);

        var stats = db.GetStorageStats();
        Assert.Equal(2048, stats.MaxLimitMb);
        Assert.True(stats.AutoPrune);
        Assert.True(stats.UsedBytes > 0);
    }

    [Fact]
    public void Reports_GenerateValidPdfAndMultiSheetXlsx()
    {
        byte[] jan = MakeValidMonthBytes("202601", 2_000_000, 3_200_000);
        var doc = SintegraParser.ParseBytes(jan);
        var rep = SintegraValidator.Validate(doc);
        var xmls = new[]
        {
            new XmlDocumentSummary("nf999.xml", "NFe", "CHAVE_OMISSA", "55", "1", "999", "20260115",
                "00000000000191", "FORNECEDOR X", "11222333000181", "1", "1", "COMPRA", false,
                150_000, 150_000, 18_000, new[] { "5102" }, System.Array.Empty<string>())
        };
        var audit = FiscalCrossAuditor.RunCrossAudit(doc, xmls, null);
        var scoped = PeriodFilter.CreateScopedView(doc, rep, audit, "ALL");
        var ctx = new FiscalReportContext(doc, rep, scoped, audit, "Simples Nacional", "1.0.0");

        string pdfPath = Path.Combine(_tempDir, "relatorio.pdf");
        PdfReportGenerator.GeneratePdf(ctx, pdfPath);
        Assert.True(File.Exists(pdfPath));
        byte[] pdfBytes = File.ReadAllBytes(pdfPath);
        Assert.True(pdfBytes.Length > 1000);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdfBytes, 0, 5));

        string xlsxPath = Path.Combine(_tempDir, "planilha.xlsx");
        ExcelReportGenerator.GenerateWorkbook(ctx, xlsxPath);
        Assert.True(File.Exists(xlsxPath));
        using var wb = new XLWorkbook(xlsxPath);
        Assert.Contains(wb.Worksheets, ws => ws.Name == "Resumo Executivo");
        Assert.Contains(wb.Worksheets, ws => ws.Name == "Movimentacao Mensal");
        Assert.Contains(wb.Worksheets, ws => ws.Name == "Apuracao por CFOP");
        Assert.Contains(wb.Worksheets, ws => ws.Name == "Notas Reg 50");
        Assert.Contains(wb.Worksheets, ws => ws.Name == "Saidas NFC-e Reg 61");
        Assert.Contains(wb.Worksheets, ws => ws.Name == "Confronto XML");
        Assert.Contains(wb.Worksheets, ws => ws.Name == "Diagnostico");
    }
}
