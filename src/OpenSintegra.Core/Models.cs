namespace OpenSintegra.Core;

public enum Severity
{
    Error,
    Warning,
    Info
}

public sealed record Diagnostic(
    string Code,
    Severity Severity,
    int Line,
    int ColumnStart,
    int ColumnEnd,
    string RecordType,
    string FieldName,
    string Message,
    string ActualValue,
    string ExpectedHint,
    bool AutoFixable
)
{
    public string SeverityLabel => Severity switch
    {
        Severity.Error => "ERRO",
        Severity.Warning => "AVISO",
        _ => "INFO"
    };
}

public sealed record FieldSlice(
    int Index,
    int StartCol,
    int EndCol,
    string Name,
    string RawValue,
    string FormattedValue
)
{
    public int Length => EndCol - StartCol + 1;
    public string PositionRange => $"{StartCol:000}..{EndCol:000}";
}

public sealed record RawLine(
    int LineNumber,
    string RecordType,
    string? Subtype,
    string RawText,
    int CharLen,
    bool HasCr,
    bool HasLf
);

public sealed record Reg10(
    int Line,
    string Cnpj,
    string Ie,
    string NomeContribuinte,
    string Municipio,
    string Uf,
    string Fax,
    string DataInicial,
    string DataFinal,
    string CodEstrutura,
    string CodNatureza,
    string CodFinalidade
);

public sealed record Reg11(
    int Line,
    string Logradouro,
    string Numero,
    string Complemento,
    string Bairro,
    string Cep,
    string NomeContato,
    string Telefone
);

public sealed record Reg50(
    int Line,
    string Cnpj,
    string Ie,
    string DataEmissao,
    string Uf,
    string Modelo,
    string Serie,
    string Numero,
    string Cfop,
    string Emitente,
    long ValorTotalCents,
    long BaseIcmsCents,
    long ValorIcmsCents,
    long IsentaNaoTribCents,
    long OutrasCents,
    long AliquotaBps,
    string Situacao
);

public sealed record Reg51(
    int Line,
    string Cnpj,
    string Ie,
    string DataEmissao,
    string Uf,
    string Serie,
    string Numero,
    string Cfop,
    long ValorTotalCents,
    long ValorIpiCents,
    long IsentaIpiCents,
    long OutrasIpiCents,
    string Situacao
);

public sealed record Reg53(
    int Line,
    string Cnpj,
    string Ie,
    string DataEmissao,
    string Uf,
    string Modelo,
    string Serie,
    string Numero,
    string Cfop,
    string Emitente,
    long BaseStCents,
    long IcmsRetidoCents,
    long DespesasAcessoriasCents,
    string Situacao,
    string CodAntecipacao
);

public sealed record Reg54(
    int Line,
    string Cnpj,
    string Modelo,
    string Serie,
    string Numero,
    string Cfop,
    string Cst,
    string NumItem,
    string CodProduto,
    long QuantidadeMilli,
    long ValorProdutoCents,
    long ValorDescontoCents,
    long BaseIcmsCents,
    long BaseStCents,
    long ValorIpiCents,
    long AliquotaBps
);

public sealed record Reg60M(
    int Line,
    string DataEmissao,
    string NumSerieFab,
    string NumOrdemEqp,
    string ModeloDoc,
    string CooInicial,
    string CooFinal,
    string Crz,
    string Cro,
    long VendaBrutaCents,
    long GtFinalCents
);

public sealed record Reg60A(
    int Line,
    string DataEmissao,
    string NumSerieFab,
    string SituacaoTributaria,
    long ValorAcumuladoCents
);

public sealed record Reg60D(
    int Line,
    string DataEmissao,
    string NumSerieFab,
    string CodProduto,
    long QuantidadeMilli,
    long ValorLiquidoCents,
    long BaseIcmsCents,
    string SituacaoTributaria,
    long ValorIcmsCents
);

public sealed record Reg60I(
    int Line,
    string DataEmissao,
    string NumSerieFab,
    string ModeloDoc,
    string Coo,
    string NumItem,
    string CodProduto,
    long QuantidadeMilli,
    long ValorLiquidoCents,
    long BaseIcmsCents,
    string SituacaoTributaria,
    long ValorIcmsCents
);

public sealed record Reg60R(
    int Line,
    string MesAno,
    string CodProduto,
    long QuantidadeMilli,
    long ValorLiquidoCents,
    long BaseIcmsCents,
    long AliquotaBps
);

public sealed record Reg61(
    int Line,
    string DataEmissao,
    string Modelo,
    string Serie,
    string Subserie,
    string NumInicialOrdem,
    string NumFinalOrdem,
    long ValorTotalCents,
    long BaseIcmsCents,
    long ValorIcmsCents,
    long IsentaCents,
    long OutrasCents,
    long AliquotaBps
);

public sealed record Reg61R(
    int Line,
    string MesAno,
    string CodProduto,
    long QuantidadeMilli,
    long ValorBrutoCents,
    long BaseIcmsCents,
    long AliquotaBps
);

public sealed record Reg70(
    int Line,
    string Cnpj,
    string Ie,
    string DataEmissao,
    string Uf,
    string Modelo,
    string Serie,
    string Subserie,
    string Numero,
    string Cfop,
    long ValorTotalCents,
    long BaseIcmsCents,
    long ValorIcmsCents,
    long IsentaCents,
    long OutrasCents,
    string CifFob,
    string Situacao
);

public sealed record Reg74(
    int Line,
    string DataInventario,
    string CodProduto,
    long QuantidadeMilli,
    long ValorProdutoCents,
    string CodPosse,
    string CnpjPossuidor,
    string IePossuidor,
    string UfPossuidor
);

public sealed record Reg75(
    int Line,
    string DataInicial,
    string DataFinal,
    string CodProduto,
    string CodNcm,
    string Descricao,
    string Unidade,
    long AliquotaIpiBps,
    long AliquotaIcmsBps,
    long ReducaoBaseIcmsBps,
    long BaseStCents
);

public sealed record Reg90Counter(string TipoATotalizar, long TotalDeclarado);

public sealed record Reg90(
    int Line,
    string Cnpj,
    string Ie,
    IReadOnlyList<Reg90Counter> Counters,
    string NumReg90
);

public sealed class CfopSummaryRow
{
    public string Cfop { get; init; } = "";
    public string Nature { get; init; } = "";
    public int DocumentCount { get; set; }
    public long ValorContabilCents { get; set; }
    public long BaseIcmsCents { get; set; }
    public long ValorIcmsCents { get; set; }
    public long IsentaCents { get; set; }
    public long OutrasCents { get; set; }
    public long ValorIpiCents { get; set; }
    public long BaseStCents { get; set; }
    public long IcmsRetidoCents { get; set; }
}

public sealed record SequenceGap(
    string Modelo,
    string Serie,
    long FromNumber,
    long ToNumber,
    long MissingCount
);

public sealed class SintegraDocument
{
    public int ByteSize { get; init; }
    public string Crc32Hex { get; init; } = "";
    public string Sha256Hex { get; init; } = "";
    public List<RawLine> Lines { get; init; } = new();
    public Reg10? Reg10 { get; set; }
    public Reg11? Reg11 { get; set; }
    public List<Reg50> Reg50 { get; init; } = new();
    public List<Reg51> Reg51 { get; init; } = new();
    public List<Reg53> Reg53 { get; init; } = new();
    public List<Reg54> Reg54 { get; init; } = new();
    public List<Reg60M> Reg60M { get; init; } = new();
    public List<Reg60A> Reg60A { get; init; } = new();
    public List<Reg60D> Reg60D { get; init; } = new();
    public List<Reg60I> Reg60I { get; init; } = new();
    public List<Reg60R> Reg60R { get; init; } = new();
    public List<Reg61> Reg61 { get; init; } = new();
    public List<Reg61R> Reg61R { get; init; } = new();
    public List<Reg70> Reg70 { get; init; } = new();
    public List<Reg74> Reg74 { get; init; } = new();
    public List<Reg75> Reg75 { get; init; } = new();
    public List<Reg90> Reg90 { get; init; } = new();
    public HashSet<string> OtherProductCodes { get; init; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> PhysicalCounts { get; init; } = new();
}

public sealed record MonthlyBalanceRow(
    string YearMonth,
    string MonthLabel,
    int Reg50EntradasDocs,
    long ComprasEntradasCents,
    long OutrasEntradasCents,
    long TotalEntradasCents,
    long IcmsEntradasCents,
    int Reg50SaidasDocs,
    long SaidasReg50Cents,
    int Reg61SaidasLines,
    long Nfce61QtyDocs,
    long SaidasReg61Cents,
    long SaidasReg60MCents,
    long TotalSaidasCents,
    long IcmsSaidasCents,
    long SaldoLiquidoCents,
    double? SaidaSobreEntradaPct
);

public sealed record ValidationSummary(
    bool Valid,
    int ErrorCount,
    int WarningCount,
    int InfoCount,
    int TotalLines,
    int ByteSize,
    string Crc32Hex,
    string Sha256Hex,
    int TotalInvoices50,
    int TotalItems54,
    int TotalProducts75,
    int TotalTransport70,
    int TotalReg61,
    int TotalReg61R,
    bool IsMultiMonthPeriod,
    int DistinctMonthsCount,
    long ComprasEntradasCents,
    long OutrasEntradasCents,
    long ValorContabilEntradasCents,
    long SaidasReg50Cents,
    long SaidasReg61Cents,
    long SaidasReg60MCents,
    long ValorContabilSaidasCents,
    long ValorIcmsEntradasCents,
    long ValorIcmsSaidasCents
);

public sealed record ValidationReport(
    ValidationSummary Summary,
    Reg10? Reg10,
    Reg11? Reg11,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<CfopSummaryRow> CfopSummary,
    IReadOnlyList<MonthlyBalanceRow> MonthlyBalance,
    IReadOnlyList<SequenceGap> SequenceGaps,
    IReadOnlyDictionary<string, long> PhysicalCounts,
    IReadOnlyDictionary<string, long> DeclaredCounts90,
    IReadOnlyDictionary<string, int> ProductMovementCounts
);
