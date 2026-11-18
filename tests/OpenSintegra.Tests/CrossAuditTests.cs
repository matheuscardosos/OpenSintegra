using System.Collections.Generic;
using System.Text;
using OpenSintegra.Core;
using Xunit;

namespace OpenSintegra.Tests;

public sealed class CrossAuditTests
{
    [Fact]
    public void RunCrossAudit_DetectsOmittedPurchase_AndIgnoresComodatoAndReturnedNfe()
    {
        var sb = new StringBuilder();
        sb.Append(SintegraFixtureBuilder.Reg10("11222333000181", "ISENTO", "EMPRESA TESTE COMERCIAL", "20260101", "20260131")).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg11()).Append("\r\n");
        // Launched NF 1001001 -> Sintegra 6 digits "001001"
        sb.Append(SintegraFixtureBuilder.Reg50("00000000000191", "20260110", "001001", "1102", 1_250_000)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg90("11222333000181", "ISENTO", ("50", 1), ("99", 4))).Append("\r\n");

        var doc = SintegraParser.ParseBytes(Encoding.Latin1.GetBytes(sb.ToString()));

        var xmls = new List<XmlDocumentSummary>
        {
            // Purchase NF 1001001 -> matches modulo 1M ("001001")
            new("nf1001001.xml", "NFe", "CHAVE_LANCADA", "55", "1", "1001001", "20260110",
                "00000000000191", "FORNECEDOR A", "11222333000181", "1", "1", "COMPRA", false,
                1250000, 1250000, 150000, new[] { "5102" }, System.Array.Empty<string>()),

            // Purchase NF 2002 -> Omitted in Sintegra!
            new("nf2002.xml", "NFe", "CHAVE_OMISSA", "55", "1", "2002", "20260112",
                "00000000000191", "FORNECEDOR B", "11222333000181", "1", "1", "COMPRA", false,
                345000, 345000, 41400, new[] { "5102" }, System.Array.Empty<string>()),

            // Purchase NF 3003 -> Was returned via tpNF=0 referencing CHAVE_DEVOLVIDA
            new("nf3003.xml", "NFe", "CHAVE_DEVOLVIDA", "55", "1", "3003", "20260113",
                "00000000000191", "FORNECEDOR B", "11222333000181", "1", "1", "COMPRA", false,
                99000, 99000, 0, new[] { "5102" }, System.Array.Empty<string>()),

            // Return NFe referencing CHAVE_DEVOLVIDA
            new("ret3004.xml", "NFe", "CHAVE_RETORNO", "55", "1", "3004", "20260114",
                "00000000000191", "FORNECEDOR B", "11222333000181", "0", "4", "DEVOLUCAO", false,
                99000, 99000, 0, new[] { "1202" }, new[] { "CHAVE_DEVOLVIDA" }),

            // Comodato CFOP 5908
            new("comod5005.xml", "NFe", "CHAVE_COMODATO", "55", "1", "5005", "20260115",
                "00000000000191", "FORNECEDOR C", "11222333000181", "1", "1", "REMESSA COMODATO", false,
                500000, 0, 0, new[] { "5908" }, System.Array.Empty<string>())
        };

        var audit = FiscalCrossAuditor.RunCrossAudit(doc, xmls, null);

        Assert.Equal(2, audit.XmlComprasTotal);
        Assert.Equal(1, audit.XmlComprasLancadas);
        Assert.Equal(1, audit.XmlComprasOmissas);
        Assert.Equal(345000L, audit.XmlComprasOmissasCents);
        Assert.Contains(audit.DocumentRows, r => r.Numero == "2002" && r.IsCriticalGap);
        Assert.DoesNotContain(audit.DocumentRows, r => r.Numero == "3003" && r.IsCriticalGap);
        Assert.DoesNotContain(audit.DocumentRows, r => r.Numero == "5005" && r.IsCriticalGap);
    }

    [Fact]
    public void RunCrossAudit_FindsInvoiceLaunchedInLaterMonthAcrossFullAnnualSintegra()
    {
        var sb = new StringBuilder();
        sb.Append(SintegraFixtureBuilder.Reg10("11222333000181", "ISENTO", "EMPRESA ANUAL CRUZAMENTO", "20260401", "20260531")).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg11()).Append("\r\n");
        // Emitted on 30/04/2026 in XML, entered into Sintegra on 02/05/2026
        sb.Append(SintegraFixtureBuilder.Reg50("00000000000191", "20260502", "004500", "1102", 85_000)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg90("11222333000181", "ISENTO", ("50", 1), ("99", 4))).Append("\r\n");

        var doc = SintegraParser.ParseBytes(Encoding.Latin1.GetBytes(sb.ToString()));
        var xmls = new List<XmlDocumentSummary>
        {
            new("nf4500.xml", "NFe", "CHAVE_4500", "55", "1", "4500", "20260430",
                "00000000000191", "FORNECEDOR MES SEGUINTE", "11222333000181", "1", "1", "COMPRA", false,
                85_000, 85_000, 10_200, new[] { "5102" }, System.Array.Empty<string>())
        };

        var audit = FiscalCrossAuditor.RunCrossAudit(doc, xmls, null);
        Assert.Equal(0, audit.XmlComprasOmissas);
        Assert.Equal(1, audit.XmlComprasLancadas);
        var row = Assert.Single(audit.DocumentRows);
        Assert.Equal("Lançada OK", row.MatchStatus);
        Assert.False(row.IsCriticalGap);
        Assert.Contains("05/2026", row.Notes);
        Assert.Contains(audit.Findings, f => f.Code == "AUD-XML-006");
    }
}
