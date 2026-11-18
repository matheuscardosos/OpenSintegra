using System.Linq;
using OpenSintegra.Core;
using Xunit;

namespace OpenSintegra.Tests;

public sealed class TaxIdAndDecomposerTests
{
    [Theory]
    [InlineData("11222333000181", true)]
    [InlineData("00000000000191", true)]
    [InlineData("11222333000182", false)]
    [InlineData("11111111111111", false)]
    [InlineData("12345", false)]
    public void ValidateCnpj_ChecksDigitsAndLength(string cnpj, bool expected)
    {
        Assert.Equal(expected, TaxIdValidator.ValidateCnpj(cnpj));
    }

    [Theory]
    [InlineData("1102", true, true)]
    [InlineData("1403", true, true)]
    [InlineData("1910", true, false)]
    [InlineData("1411", true, false)]
    [InlineData("5102", false, false)]
    [InlineData("6102", false, false)]
    public void CfopClassification_IdentifiesEntradaAndCompraComercial(
        string cfop, bool expectedEntrada, bool expectedCompra)
    {
        Assert.Equal(expectedEntrada, TaxIdValidator.IsEntradaCfop(cfop));
        Assert.Equal(expectedCompra, TaxIdValidator.IsCompraComercialCfop(cfop));
    }

    [Fact]
    public void FormattingHelpers_ProduceBrazilianFormats()
    {
        Assert.Equal("11.222.333/0001-81", TaxIdValidator.FormatCnpj("11222333000181"));
        Assert.Equal("17/11/2026", TaxIdValidator.FormatDateBr("20261117"));
        Assert.Equal("05/2026", TaxIdValidator.FormatMonthLabel("202605"));
        Assert.Equal("1.234,56", TaxIdValidator.FormatCentsBrl(123456));
        Assert.Equal("R$ 47.013,45", TaxIdValidator.FormatSignedCentsBrl(4701345));
        Assert.Equal("- R$ 10.585,76", TaxIdValidator.FormatSignedCentsBrl(-1058576));
        Assert.Equal("R$ 0,00", TaxIdValidator.FormatSignedCentsBrl(0));
    }

    [Fact]
    public void RecordSchemaDecomposer_SlicesOfficial126ColReg10()
    {
        string r10 = SintegraFixtureBuilder.Reg10("11222333000181", "ISENTO", "EMPRESA TESTE LTDA", "20260101", "20260131");
        Assert.Equal(126, r10.Length);

        var fields = RecordSchemaDecomposer.DecomposeLine(r10);
        Assert.Contains(fields, f => f.Name.Contains("CNPJ", System.StringComparison.Ordinal) && f.RawValue == "11222333000181");
        Assert.Contains(fields, f => f.Name.Contains("UF", System.StringComparison.Ordinal) && f.RawValue == "MS");
        Assert.Contains(fields, f => f.Name.Contains("Data Inicial", System.StringComparison.Ordinal) && f.FormattedValue == "01/01/2026");
    }
}
