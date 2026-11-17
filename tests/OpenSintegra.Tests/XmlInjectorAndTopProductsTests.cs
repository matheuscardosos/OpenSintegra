using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenSintegra.Core;
using OpenSintegra.Data;
using Xunit;

namespace OpenSintegra.Tests;

public sealed class XmlInjectorAndTopProductsTests
{
    [Fact]
    public void SuggestEntryCfop_MapsSameStateAndInterstatePurchasesCleanly()
    {
        Assert.Equal("1403", SintegraXmlInjector.SuggestEntryCfop("5405", "MG", "MG"));
        Assert.Equal("2403", SintegraXmlInjector.SuggestEntryCfop("6403", "SP", "MG"));
        Assert.Equal("1102", SintegraXmlInjector.SuggestEntryCfop("5102", "MG", "MG"));
        Assert.Equal("2102", SintegraXmlInjector.SuggestEntryCfop("6102", "SP", "MG"));
        Assert.Equal("1910", SintegraXmlInjector.SuggestEntryCfop("5910", "MG", "MG"));
    }

    [Fact]
    public void InjectXmlInvoices_AddsMissingPurchaseWithItemsAndProducesZeroValidationErrors()
    {
        var sb = new StringBuilder();
        sb.Append(SintegraFixtureBuilder.Reg10("11222333000181", "ISENTO", "ACOUGUE TESTE LTDA", "20260101", "20260131")).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg11()).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg61("20260131", 500_000)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg90("11222333000181", "ISENTO", ("61", 1), ("99", 4))).Append("\r\n");
        byte[] initialBytes = Encoding.Latin1.GetBytes(sb.ToString());

        string nfeXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
              <NFe>
                <infNFe Id="NFe31260100000000000191550010000055551000000011" versao="4.00">
                  <ide>
                    <mod>55</mod>
                    <serie>1</serie>
                    <nNF>5555</nNF>
                    <dhEmi>2026-01-20T10:00:00-03:00</dhEmi>
                    <tpNF>1</tpNF>
                    <finNFe>1</finNFe>
                    <natOp>VENDA DE MERCADORIA</natOp>
                  </ide>
                  <emit>
                    <CNPJ>00000000000191</CNPJ>
                    <xNome>FRIGORIFICO FORNECEDOR SA</xNome>
                    <enderEmit><UF>MG</UF></enderEmit>
                    <IE>ISENTO</IE>
                  </emit>
                  <dest>
                    <CNPJ>11222333000181</CNPJ>
                  </dest>
                  <det nItem="1">
                    <prod>
                      <cProd>CARNE01</cProd>
                      <xProd>ALCATRA RESFRIADA KG</xProd>
                      <NCM>02013000</NCM>
                      <CFOP>5102</CFOP>
                      <uCom>KG</uCom>
                      <qCom>10.0000</qCom>
                      <vProd>350.00</vProd>
                    </prod>
                    <imposto>
                      <ICMS><ICMSSN102><CSOSN>102</CSOSN></ICMSSN102></ICMS>
                    </imposto>
                  </det>
                  <total>
                    <ICMSTot>
                      <vBC>0.00</vBC>
                      <vICMS>0.00</vICMS>
                      <vProd>350.00</vProd>
                      <vFrete>15.00</vFrete>
                      <vNF>365.00</vNF>
                    </ICMSTot>
                  </total>
                </infNFe>
              </NFe>
            </nfeProc>
            """;

        var xmlDoc = FiscalCrossAuditor.ParseFiscalXml("nfe_5555.xml", nfeXml);
        Assert.NotNull(xmlDoc);
        Assert.NotNull(xmlDoc!.FullData);
        Assert.Single(xmlDoc.FullData!.Items);

        // First audit shows 1 omitted purchase
        var preDoc = SintegraParser.ParseBytes(initialBytes);
        var preAudit = FiscalCrossAuditor.RunCrossAudit(preDoc, new[] { xmlDoc }, null);
        Assert.Equal(1, preAudit.XmlComprasOmissas);

        // Inject omitted NF directly
        var result = SintegraXmlInjector.InjectXmlInvoices(
            initialBytes,
            new[] { new XmlInjectionRequest(xmlDoc, "1102") },
            simplesNacionalZeroIcms: true,
            includeReg54Items: true);

        Assert.Equal(1, result.InjectedInvoicesCount);
        Assert.Equal(0, result.AfterReport.Summary.ErrorCount);

        // Re-audit proves 0 omitted purchases and totals increased by R$ 365,00
        var afterDoc = SintegraParser.ParseBytes(result.UpdatedBytes);
        var postAudit = FiscalCrossAuditor.RunCrossAudit(afterDoc, new[] { xmlDoc }, null);
        Assert.Equal(0, postAudit.XmlComprasOmissas);
        Assert.Equal(1, postAudit.XmlComprasLancadas);
        Assert.Equal(36_500L, result.AfterReport.Summary.ValorContabilEntradasCents);
        Assert.Contains(afterDoc.Reg75, p => p.CodProduto == "CARNE01" && p.Descricao.Contains("ALCATRA"));
    }

    [Fact]
    public void PeriodFilter_AggregatesTopSellingProductsFromReg61R()
    {
        var sb = new StringBuilder();
        sb.Append(SintegraFixtureBuilder.Reg10("11222333000181", "ISENTO", "ACOUGUE TESTE LTDA", "20260101", "20260228")).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg11()).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg61("20260131", 800_000)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg61("20260228", 500_000)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg61R("012026", "PICANHA", 600_000)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg61R("022026", "FRALDINHA", 500_000)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg61R("012026", "LINGUICA", 200_000)).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg75("20260101", "20260228", "PICANHA", "PICANHA BOVINA ESPECIAL")).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg75("20260101", "20260228", "FRALDINHA", "FRALDINHA BOVINA")).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg75("20260101", "20260228", "LINGUICA", "LINGUICA TOSCANA")).Append("\r\n");
        sb.Append(SintegraFixtureBuilder.Reg90("11222333000181", "ISENTO", ("61", 5), ("75", 3), ("99", 12))).Append("\r\n");

        var doc = SintegraParser.ParseBytes(Encoding.Latin1.GetBytes(sb.ToString()));
        var rep = SintegraValidator.Validate(doc);

        var allScoped = PeriodFilter.CreateScopedView(doc, rep, null, "ALL");
        Assert.Equal(3, allScoped.TopSellingProducts.Count);
        Assert.Equal("PICANHA", allScoped.TopSellingProducts[0].CodProduto);
        Assert.Equal("PICANHA BOVINA ESPECIAL", allScoped.TopSellingProducts[0].Descricao);

        var febScoped = PeriodFilter.CreateScopedView(doc, rep, null, "202602");
        Assert.Single(febScoped.TopSellingProducts);
        Assert.Equal("FRALDINHA", febScoped.TopSellingProducts[0].CodProduto);
    }

    [Fact]
    public void FiscalDatabase_PersistsAppSettings()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"os_settings_{System.Guid.NewGuid():N}.db");
        try
        {
            var db = new FiscalDatabase(dbPath);
            Assert.Null(db.GetSetting("last_sintegra_path"));
            db.SetSetting("last_sintegra_path", @"C:\Fiscal\Sintegra_2026.txt");
            Assert.Equal(@"C:\Fiscal\Sintegra_2026.txt", db.GetSetting("last_sintegra_path"));
            db.SetSetting("last_sintegra_path", null);
            Assert.Null(db.GetSetting("last_sintegra_path"));
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }
}
