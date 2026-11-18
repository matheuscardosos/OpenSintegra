using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace OpenSintegra.Core;

public enum XmlOperationCategory
{
    CompraComercial,
    Bonificacao,
    Comodato,
    DevolucaoRetorno,
    SaidaPropria,
    NfceConsumidor,
    CteTransporte,
    NfseServico,
    EventoCancelamento
}

public sealed record XmlProductItem(
    string NumItem,
    string CodProduto,
    string Descricao,
    string Ncm,
    string Unidade,
    string OrigCfop,
    string CstCsosn,
    long QuantidadeMilli,
    long ValorBrutoCents,
    long DescontoCents,
    long BaseIcmsCents,
    long ValorIpiCents,
    long AliquotaIcmsBps
);

public sealed record XmlFullData(
    string SupplierUf,
    string SupplierIe,
    long FreteCents,
    long OutrasDespCents,
    long IcmsStCents,
    IReadOnlyList<XmlProductItem> Items
);

public sealed record XmlDocumentSummary(
    string FileName,
    string DocKind,
    string Chave,
    string Modelo,
    string Serie,
    string Numero,
    string DataEmissao,
    string EmitCnpj,
    string EmitNome,
    string DestCnpj,
    string TpNf,
    string FinNfe,
    string NatOp,
    bool Cancelled,
    long ValorTotalCents,
    long BaseIcmsCents,
    long ValorIcmsCents,
    IReadOnlyList<string> Cfops,
    IReadOnlyList<string> RefNFeKeys,
    XmlFullData? FullData = null
);

public sealed record AuditFinding(
    string Code,
    Severity Severity,
    string Source,
    string DocumentRef,
    string Message,
    string SintegraValue,
    string ExternalValue
)
{
    public string SeverityLabel => Severity switch
    {
        Severity.Error => "ERRO",
        Severity.Warning => "AVISO",
        _ => "INFO"
    };
}

public sealed record XmlAuditDocumentRow(
    string YearMonth,
    string DataBr,
    string EmitCnpj,
    string EmitNome,
    string Modelo,
    string Serie,
    string Numero,
    XmlOperationCategory Category,
    string CategoryLabel,
    string MatchStatus,
    bool IsCriticalGap,
    long XmlValorCents,
    long SintegraValorCents,
    string XmlCfops,
    string SintegraCfops,
    string Notes
);

public sealed record MonthlyXmlCoverageRow(
    string YearMonth,
    string MonthLabel,
    int XmlComprasTotal,
    long XmlComprasCents,
    int XmlComprasLancadas,
    int XmlComprasOmissas,
    long XmlComprasOmissasCents,
    int XmlOutrasNaoLancadas,
    long SintegraEntradasCents,
    long SintegraSaidasCents
);

public sealed record CfopComparisonRow(
    string Cfop,
    long SintegraContabilCents,
    long ExternalContabilCents,
    long DiffContabilCents,
    long SintegraIcmsCents,
    long ExternalIcmsCents,
    long DiffIcmsCents
);

public sealed record AuditReport(
    double PrecisionScorePct,
    int TotalExternalDocs,
    int MatchedDocs,
    int MissingInSintegra,
    int ExtraInSintegra,
    int ValueDivergences,
    int StatusDivergences,
    int NfeCount,
    int NfceCount,
    int CteCount,
    int NfseCount,
    int CancelEventsCount,
    int SpedC100Count,
    int XmlComprasTotal,
    int XmlComprasLancadas,
    int XmlComprasOmissas,
    long XmlComprasOmissasCents,
    int XmlOutrasOmissasCount,
    IReadOnlyList<AuditFinding> Findings,
    IReadOnlyList<XmlAuditDocumentRow> DocumentRows,
    IReadOnlyList<MonthlyXmlCoverageRow> MonthlyCoverage,
    IReadOnlyList<CfopComparisonRow> CfopComparison
);

public sealed class SpedInvoice
{
    public string CodMod { get; init; } = "";
    public string CodSit { get; init; } = "";
    public string Ser { get; init; } = "";
    public string NumDoc { get; init; } = "";
    public long VlDocCents { get; init; }
    public long VlIcmsCents { get; init; }
}

public sealed class SpedParsedData
{
    public string CnpjEstab { get; set; } = "";
    public List<SpedInvoice> Invoices { get; } = new();
    public SortedDictionary<string, (long VlOprCents, long VlIcmsCents)> CfopTotals { get; } = new();
    public HashSet<string> Products0200 { get; } = new(StringComparer.Ordinal);
}

public static class FiscalCrossAuditor
{
    private static long ParseDecimalToCents(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0L;
        string clean = s.Trim().Replace(',', '.');
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val)
            ? (long)Math.Round(val * 100m, MidpointRounding.AwayFromZero)
            : 0L;
    }

    private static long NormalizeDocNum(string num)
    {
        var digits = new string(num.Where(char.IsAsciiDigit).ToArray());
        return long.TryParse(digits, out long parsed) ? (parsed % 1_000_000L) : 0L;
    }

    private static string NormalizeSerie(string s)
    {
        string t = s.Trim();
        if (string.IsNullOrEmpty(t)) return "0";
        return t.All(char.IsAsciiDigit) && int.TryParse(t, out int n) ? n.ToString() : t.ToUpperInvariant();
    }

    private static XElement? Desc(XContainer parent, string localName) =>
        parent.Descendants().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    private static string ChildText(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";

    public static XmlDocumentSummary? ParseFiscalXml(string fileName, string xmlText)
    {
        try
        {
            var xdoc = XDocument.Parse(xmlText.TrimStart('\uFEFF').Trim());
            var root = xdoc.Root;
            if (root is null) return null;
            string rootName = root.Name.LocalName;

            if (rootName.Equals("procEventoNFe", StringComparison.OrdinalIgnoreCase) ||
                rootName.Equals("evento", StringComparison.OrdinalIgnoreCase) ||
                rootName.Equals("procEventoCTe", StringComparison.OrdinalIgnoreCase))
            {
                var infEv = Desc(root, "infEvento");
                if (infEv is not null)
                {
                    string tpEv = ChildText(infEv, "tpEvento");
                    string ch = ChildText(infEv, "chNFe");
                    if (string.IsNullOrEmpty(ch)) ch = ChildText(infEv, "chCTe");
                    if (tpEv is "110111" or "110112" && ch.Length == 44)
                    {
                        return new XmlDocumentSummary(
                            fileName, "EventoCancelamento", ch,
                            ch[20..22], ch[22..25], ch[25..34], "",
                            ch[6..20], "", "", "", "", "CANCELAMENTO", true,
                            0, 0, 0, Array.Empty<string>(), Array.Empty<string>());
                    }
                }
                return null;
            }

            var infNfe = Desc(root, "infNFe");
            if (infNfe is not null)
            {
                var ide = Desc(infNfe, "ide");
                var emit = Desc(infNfe, "emit");
                var dest = Desc(infNfe, "dest");
                if (ide is null || emit is null) return null;

                string modelo = ChildText(ide, "mod");
                string serie = ChildText(ide, "serie");
                string numero = ChildText(ide, "nNF");
                string tpNf = ChildText(ide, "tpNF");
                string finNfe = ChildText(ide, "finNFe");
                string natOp = ChildText(ide, "natOp");
                string dhEmi = ChildText(ide, "dhEmi");
                if (string.IsNullOrEmpty(dhEmi)) dhEmi = ChildText(ide, "dEmi");
                string dataEmi = dhEmi.Replace("-", "");
                if (dataEmi.Length > 8) dataEmi = dataEmi[..8];

                string emitCnpj = ChildText(emit, "CNPJ");
                if (string.IsNullOrEmpty(emitCnpj)) emitCnpj = ChildText(emit, "CPF");
                string emitNome = ChildText(emit, "xNome");

                string destCnpj = dest is not null ? ChildText(dest, "CNPJ") : "";
                if (string.IsNullOrEmpty(destCnpj) && dest is not null) destCnpj = ChildText(dest, "CPF");

                string chave = infNfe.Attribute("Id")?.Value.Replace("NFe", "") ?? "";
                string cStat = "100";
                var infProt = Desc(root, "infProt");
                if (infProt is not null)
                {
                    string ch = ChildText(infProt, "chNFe");
                    if (!string.IsNullOrEmpty(ch)) chave = ch;
                    string st = ChildText(infProt, "cStat");
                    if (!string.IsNullOrEmpty(st)) cStat = st;
                }
                bool cancelled = cStat is "101" or "151" or "155" or "102" or "110" or "301" or "302";

                var icmsTot = Desc(infNfe, "ICMSTot");
                long vNf = ParseDecimalToCents(ChildText(icmsTot, "vNF"));
                long vBc = ParseDecimalToCents(ChildText(icmsTot, "vBC"));
                long vIcms = ParseDecimalToCents(ChildText(icmsTot, "vICMS"));
                long vFrete = ParseDecimalToCents(ChildText(icmsTot, "vFrete"));
                long vOutro = ParseDecimalToCents(ChildText(icmsTot, "vOutro"));
                long vSt = ParseDecimalToCents(ChildText(icmsTot, "vST"));

                var enderEmit = Desc(emit, "enderEmit");
                string supplierUf = ChildText(enderEmit, "UF");
                string supplierIe = ChildText(emit, "IE");

                static long ParseMilli(string? s)
                {
                    if (string.IsNullOrWhiteSpace(s)) return 0L;
                    string clean = s.Trim().Replace(',', '.');
                    return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val)
                        ? (long)Math.Round(val * 1000m, MidpointRounding.AwayFromZero)
                        : 0L;
                }

                var cfops = new List<string>();
                var items = new List<XmlProductItem>();
                int seqItem = 0;
                foreach (var det in infNfe.Elements().Where(e => e.Name.LocalName.Equals("det", StringComparison.OrdinalIgnoreCase)))
                {
                    seqItem++;
                    var prod = Desc(det, "prod");
                    string cfop = ChildText(prod, "CFOP");
                    if (!string.IsNullOrEmpty(cfop) && !cfops.Contains(cfop))
                        cfops.Add(cfop);

                    string nItem = det.Attribute("nItem")?.Value.Trim() ?? seqItem.ToString();
                    string cProd = ChildText(prod, "cProd");
                    string xProd = ChildText(prod, "xProd");
                    string ncm = ChildText(prod, "NCM");
                    string uCom = ChildText(prod, "uCom");
                    long qMilli = ParseMilli(ChildText(prod, "qCom"));
                    long vProd = ParseDecimalToCents(ChildText(prod, "vProd"));
                    long vDesc = ParseDecimalToCents(ChildText(prod, "vDesc"));

                    var imposto = Desc(det, "imposto");
                    var icmsEl = imposto is null ? null : Desc(imposto, "ICMS");
                    var icmsInner = icmsEl?.Elements().FirstOrDefault();
                    string cst = ChildText(icmsInner, "CST");
                    if (string.IsNullOrEmpty(cst)) cst = ChildText(icmsInner, "CSOSN");
                    long itemVbc = ParseDecimalToCents(ChildText(icmsInner, "vBC"));
                    long itemPicms = ParseDecimalToCents(ChildText(icmsInner, "pICMS")); // 2 decimals = bps
                    var ipiEl = imposto is null ? null : Desc(imposto, "IPI");
                    long itemVipi = ParseDecimalToCents(ChildText(Desc(ipiEl ?? det, "IPITrib"), "vIPI"));

                    items.Add(new XmlProductItem(
                        nItem, cProd, xProd, ncm, uCom, cfop, cst,
                        qMilli, vProd, vDesc, itemVbc, itemVipi, itemPicms));
                }

                var refs = ide.Descendants()
                    .Where(e => e.Name.LocalName.Equals("refNFe", StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.Value.Trim())
                    .Where(v => v.Length == 44)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                var fullData = new XmlFullData(supplierUf, supplierIe, vFrete, vOutro, vSt, items);

                return new XmlDocumentSummary(
                    fileName, modelo == "65" ? "NFCe" : "NFe", chave,
                    modelo, serie, numero, dataEmi, emitCnpj, emitNome, destCnpj,
                    tpNf, finNfe, natOp, cancelled, vNf, vBc, vIcms, cfops, refs, fullData);
            }

            var infCte = Desc(root, "infCte");
            if (infCte is not null)
            {
                var ide = Desc(infCte, "ide");
                var emit = Desc(infCte, "emit");
                if (ide is null || emit is null) return null;
                string modelo = ChildText(ide, "mod");
                if (string.IsNullOrEmpty(modelo)) modelo = "57";
                string serie = ChildText(ide, "serie");
                string numero = ChildText(ide, "nCT");
                string cfop = ChildText(ide, "CFOP");
                string emitCnpj = ChildText(emit, "CNPJ");
                string emitNome = ChildText(emit, "xNome");
                string dhEmi = ChildText(ide, "dhEmi");
                string dataEmi = dhEmi.Replace("-", "");
                if (dataEmi.Length > 8) dataEmi = dataEmi[..8];

                var vPrest = Desc(infCte, "vPrest");
                long vTot = ParseDecimalToCents(ChildText(vPrest, "vTPrest"));

                return new XmlDocumentSummary(
                    fileName, "CTe", "", modelo, serie, numero, dataEmi,
                    emitCnpj, emitNome, "", "", "", "CTE", false, vTot, 0, 0,
                    string.IsNullOrEmpty(cfop) ? Array.Empty<string>() : new[] { cfop },
                    Array.Empty<string>());
            }

            if (rootName.Contains("nfse", StringComparison.OrdinalIgnoreCase) || Desc(root, "InfNfse") is not null)
            {
                string num = Desc(root, "Numero")?.Value.Trim() ?? "0";
                long vServ = ParseDecimalToCents(Desc(root, "ValorServicos")?.Value);
                return new XmlDocumentSummary(
                    fileName, "NFSe", "", "99", "1", num, "", "", "", "", "", "", "NFSe", false,
                    vServ, 0, 0, Array.Empty<string>(), Array.Empty<string>());
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static List<XmlDocumentSummary> LoadXmlsFromPath(
        string path,
        IProgress<(int Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var list = new List<XmlDocumentSummary>();
        if (Directory.Exists(path))
        {
            var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int total = Math.Max(1, files.Count);
            for (int idx = 0; idx < files.Count; idx++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(((idx + 1) * 100 / total, $"Processando arquivo: {Path.GetFileName(files[idx])}"));
                list.AddRange(LoadXmlsFromPath(files[idx], null, cancellationToken));
            }
            return list;
        }

        if (!File.Exists(path)) return list;

        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            var xmlEntries = archive.Entries
                .Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .ToList();
            int total = Math.Max(1, xmlEntries.Count);
            for (int idx = 0; idx < xmlEntries.Count; idx++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = xmlEntries[idx];
                if (idx % 5 == 0 || idx == xmlEntries.Count - 1)
                    progress?.Report(((idx + 1) * 100 / total, $"Processando arquivo: {entry.Name}"));
                using var sr = new StreamReader(entry.Open());
                string xml = sr.ReadToEnd();
                if (ParseFiscalXml(entry.Name, xml) is { } parsed)
                    list.Add(parsed);
            }
        }
        else if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report((100, $"Processando arquivo: {Path.GetFileName(path)}"));
            string xml = File.ReadAllText(path);
            if (ParseFiscalXml(Path.GetFileName(path), xml) is { } parsed)
                list.Add(parsed);
        }
        return list;
    }

    public static SpedParsedData ParseSpedFiscalFile(string path)
    {
        var data = new SpedParsedData();
        if (!File.Exists(path)) return data;

        foreach (string rawLine in File.ReadLines(path, System.Text.Encoding.Latin1))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith('|')) continue;
            string[] p = line.Split('|');
            if (p.Length < 3) continue;

            switch (p[1])
            {
                case "0000" when p.Length >= 8:
                    data.CnpjEstab = p[7].Trim();
                    break;
                case "0200" when p.Length >= 3:
                    data.Products0200.Add(p[2].Trim());
                    break;
                case "C100" when p.Length >= 23:
                    data.Invoices.Add(new SpedInvoice
                    {
                        CodMod = p[5].Trim(),
                        CodSit = p[6].Trim(),
                        Ser = p[7].Trim(),
                        NumDoc = p[8].Trim(),
                        VlDocCents = ParseDecimalToCents(p[12]),
                        VlIcmsCents = ParseDecimalToCents(p[22])
                    });
                    break;
                case "C190" or "D190" when p.Length >= 8:
                    string cfop = p[3].Trim();
                    long vlOpr = ParseDecimalToCents(p[5]);
                    long vlIcms = ParseDecimalToCents(p[7]);
                    data.CfopTotals.TryGetValue(cfop, out var cur);
                    data.CfopTotals[cfop] = (cur.VlOprCents + vlOpr, cur.VlIcmsCents + vlIcms);
                    break;
            }
        }
        return data;
    }

    private static XmlOperationCategory ClassifyXml(XmlDocumentSummary x, string estabCnpj)
    {
        if (x.DocKind == "EventoCancelamento") return XmlOperationCategory.EventoCancelamento;
        if (x.DocKind == "NFCe") return XmlOperationCategory.NfceConsumidor;
        if (x.DocKind == "CTe") return XmlOperationCategory.CteTransporte;
        if (x.DocKind == "NFSe") return XmlOperationCategory.NfseServico;

        bool isOwnEmit = !string.IsNullOrEmpty(estabCnpj) && x.EmitCnpj == estabCnpj;
        if (x.TpNf == "0" && !isOwnEmit)
            return XmlOperationCategory.DevolucaoRetorno;
        if (isOwnEmit && x.TpNf == "1")
            return XmlOperationCategory.SaidaPropria;

        if (x.Cfops.Count > 0)
        {
            if (x.Cfops.All(c => c is "5908" or "6908" or "1908" or "2908"))
                return XmlOperationCategory.Comodato;
            if (x.Cfops.All(c => c is "5910" or "6910" or "1910" or "2910" or "5911" or "6911"))
                return XmlOperationCategory.Bonificacao;
        }
        return XmlOperationCategory.CompraComercial;
    }

    private static string CategoryLabel(XmlOperationCategory cat) => cat switch
    {
        XmlOperationCategory.CompraComercial => "Compra Comercial",
        XmlOperationCategory.Bonificacao => "Bonificação / Brinde (5910)",
        XmlOperationCategory.Comodato => "Comodato (5908)",
        XmlOperationCategory.DevolucaoRetorno => "Devolução / Retorno Fornec. (tpNF=0)",
        XmlOperationCategory.SaidaPropria => "Saída Própria",
        XmlOperationCategory.NfceConsumidor => "NFC-e Consumidor (Mod 65)",
        XmlOperationCategory.CteTransporte => "CT-e Transporte",
        XmlOperationCategory.NfseServico => "NFS-e Serviço",
        _ => "Evento"
    };

    public static AuditReport RunCrossAudit(
        SintegraDocument sintegra,
        IReadOnlyList<XmlDocumentSummary> xmlDocs,
        SpedParsedData? spedData)
    {
        var findings = new List<AuditFinding>();
        var documentRows = new List<XmlAuditDocumentRow>();
        var sintReport = SintegraValidator.Validate(sintegra);
        string estabCnpj = sintegra.Reg10?.Cnpj.Trim() ?? "";

        int matchedDocs = 0, missingInSintegra = 0, extraInSintegra = 0, valueDiffs = 0, statusDiffs = 0;
        int xmlComprasTotal = 0, xmlComprasLancadas = 0, xmlComprasOmissas = 0, xmlOutrasOmissas = 0;
        long xmlComprasOmissasCents = 0;

        // Index Reg 50 across ALL months by:
        // 1) exact (Cnpj + Mod + Serie + NumMod1M)
        // 2) (Cnpj + NumMod1M) when Modelo/Serie was typed slightly differently in the ERP
        // 3) fallback (Mod + Serie + NumMod1M) when XML issuer CNPJ is absent
        var sint50ByFullKey = new Dictionary<string, List<Reg50>>(StringComparer.Ordinal);
        var sint50ByCnpjNum = new Dictionary<string, List<Reg50>>(StringComparer.Ordinal);
        var sint50ByDocKey = new Dictionary<string, List<Reg50>>(StringComparer.Ordinal);
        foreach (var r50 in sintegra.Reg50)
        {
            string mod = r50.Modelo.Trim();
            string ser = NormalizeSerie(r50.Serie);
            long num = NormalizeDocNum(r50.Numero);
            string cnpjTrim = r50.Cnpj.Trim();
            string fullKey = $"{cnpjTrim}|{mod}|{ser}|{num}";
            string cnpjNumKey = $"{cnpjTrim}|{num}";
            string docKey = $"{mod}|{ser}|{num}";

            static void AddMap(Dictionary<string, List<Reg50>> map, string k, Reg50 row)
            {
                if (!map.TryGetValue(k, out var l))
                {
                    l = new List<Reg50>();
                    map[k] = l;
                }
                l.Add(row);
            }

            AddMap(sint50ByFullKey, fullKey, r50);
            if (!string.IsNullOrEmpty(cnpjTrim))
                AddMap(sint50ByCnpjNum, cnpjNumKey, r50);
            AddMap(sint50ByDocKey, docKey, r50);
        }

        var sint70ByDocKey = new Dictionary<string, List<Reg70>>(StringComparer.Ordinal);
        foreach (var r70 in sintegra.Reg70)
        {
            string docKey = $"{r70.Modelo.Trim()}|{NormalizeSerie(r70.Serie)}|{NormalizeDocNum(r70.Numero)}";
            if (!sint70ByDocKey.TryGetValue(docKey, out var list))
            {
                list = new List<Reg70>();
                sint70ByDocKey[docKey] = list;
            }
            list.Add(r70);
        }

        // Map return XML refNFe -> returning NF number (e.g., NF 4405 devolve NF 389887)
        var returnedByMap = new Dictionary<string, XmlDocumentSummary>(StringComparer.Ordinal);
        var cancelledDocKeys = new HashSet<string>(StringComparer.Ordinal);
        int nfeCount = 0, nfceCount = 0, cteCount = 0, nfseCount = 0, cancelEventsCount = 0;

        foreach (var x in xmlDocs)
        {
            if (x.DocKind == "EventoCancelamento")
            {
                cancelEventsCount++;
                cancelledDocKeys.Add($"{x.EmitCnpj.Trim()}|{x.Modelo.Trim()}|{NormalizeSerie(x.Serie)}|{NormalizeDocNum(x.Numero)}");
            }
            if (x.TpNf == "0")
            {
                foreach (string refKey in x.RefNFeKeys)
                    returnedByMap.TryAdd(refKey, x);
            }
        }

        var matchedSint50FullKeys = new HashSet<string>(StringComparer.Ordinal);
        var matchedSint50Lines = new HashSet<int>();
        var extCfopTotals = new SortedDictionary<string, (long Contabil, long Icms)>(StringComparer.Ordinal);

        // Monthly XML coverage map
        var monthXmlMap = new SortedDictionary<string, (int ComprasTot, long ComprasCents, int Lancadas, int Omissas, long OmissasCents, int OutrasOmissas)>();
        foreach (var mb in sintReport.MonthlyBalance)
            monthXmlMap[mb.YearMonth] = (0, 0, 0, 0, 0, 0);

        foreach (var x in xmlDocs)
        {
            switch (x.DocKind)
            {
                case "NFe": nfeCount++; break;
                case "NFCe": nfceCount++; break;
                case "CTe": cteCount++; break;
                case "NFSe": nfseCount++; break;
                default: continue;
            }

            var cat = ClassifyXml(x, estabCnpj);
            string catLabel = CategoryLabel(cat);
            string mod = x.Modelo.Trim();
            string ser = NormalizeSerie(x.Serie);
            long num = NormalizeDocNum(x.Numero);
            string fullKey = $"{x.EmitCnpj.Trim()}|{mod}|{ser}|{num}";
            string docKey = $"{mod}|{ser}|{num}";
            string ym = x.DataEmissao.Length >= 6 ? x.DataEmissao[..6] : "000000";
            string dataBr = TaxIdValidator.FormatDateBr(x.DataEmissao);
            bool isCancelled = x.Cancelled || cancelledDocKeys.Contains(fullKey);

            if (x.DocKind == "NFCe")
            {
                // Match against Reg 61 (Mod 65 daily ranges)
                long docNumberLong = long.TryParse(x.Numero.Trim(), out long parsedN) ? parsedN : -1;
                bool foundIn61 = docNumberLong > 0 && sintegra.Reg61.Any(r61 =>
                    r61.Modelo == "65" &&
                    r61.DataEmissao == x.DataEmissao &&
                    long.TryParse(r61.NumInicialOrdem.Trim(), out long ini) &&
                    long.TryParse(r61.NumFinalOrdem.Trim(), out long fin) &&
                    docNumberLong >= ini && docNumberLong <= fin);

                if (foundIn61) matchedDocs++;
                documentRows.Add(new XmlAuditDocumentRow(
                    ym, dataBr, x.EmitCnpj, x.EmitNome, mod, x.Serie, x.Numero,
                    cat, catLabel, foundIn61 ? "Faixa no Reg 61" : "Não localizado no Reg 61",
                    !foundIn61 && !isCancelled, x.ValorTotalCents, 0,
                    string.Join(", ", x.Cfops), "", "NFC-e agrupada no Registro 61"));
                continue;
            }

            if (x.DocKind == "NFSe")
                continue;

            if (x.DocKind == "NFe")
            {
                List<Reg50>? r50List = null;
                string? matchedKey = null;
                bool matchedWithModelSerieDiff = false;
                if (sint50ByFullKey.TryGetValue(fullKey, out var exactList))
                {
                    r50List = exactList;
                    matchedKey = fullKey;
                }
                else if (!string.IsNullOrEmpty(x.EmitCnpj.Trim()) &&
                         sint50ByCnpjNum.TryGetValue($"{x.EmitCnpj.Trim()}|{num}", out var cnpjNumList))
                {
                    r50List = cnpjNumList;
                    matchedKey = $"{x.EmitCnpj.Trim()}|{cnpjNumList[0].Modelo.Trim()}|{NormalizeSerie(cnpjNumList[0].Serie)}|{num}";
                    matchedWithModelSerieDiff = true;
                }
                else if (string.IsNullOrEmpty(x.EmitCnpj) && sint50ByDocKey.TryGetValue(docKey, out var fbList))
                {
                    r50List = fbList;
                    matchedKey = $"{fbList[0].Cnpj.Trim()}|{docKey}";
                }

                if (r50List is not null && matchedKey is not null)
                {
                    matchedSint50FullKeys.Add(matchedKey);
                    foreach (var r in r50List) matchedSint50Lines.Add(r.Line);
                    matchedDocs++;
                    if (cat == XmlOperationCategory.CompraComercial)
                        xmlComprasTotal++;

                    bool sintCancelled = r50List.All(r => r.Situacao == "S");
                    long sintTot = r50List.Sum(r => r.Situacao == "S" ? 0L : r.ValorTotalCents);
                    long sintIcms = r50List.Sum(r => r.Situacao == "S" ? 0L : r.ValorIcmsCents);
                    string sintCfops = string.Join(", ", r50List.Select(r => r.Cfop).Distinct());

                    string? sintYm = r50List[0].DataEmissao.Length >= 6 ? r50List[0].DataEmissao[..6] : null;
                    bool launchedInDifferentMonth = sintYm is not null && ym != "000000" && sintYm != ym;
                    string sintDateBr = TaxIdValidator.FormatDateBr(r50List[0].DataEmissao);
                    string sintMonthLbl = sintYm is not null ? TaxIdValidator.FormatMonthLabel(sintYm) : "-";

                    string matchStatus = "Lançada OK";
                    bool critical = false;
                    string note = "";

                    if (isCancelled && !sintCancelled)
                    {
                        statusDiffs++;
                        matchStatus = "Cancelada no XML (Ativa no Sintegra)";
                        critical = true;
                        note = "NF cancelada no XML/SEFAZ, mas ativa no Sintegra";
                        findings.Add(new AuditFinding(
                            "AUD-XML-004", Severity.Error, "XML", $"NF-e {x.Numero} Série {x.Serie}",
                            "NF-e consta como CANCELADA no XML/Evento SEFAZ (110111), mas está ativa (Situação N) no Sintegra.",
                            $"Situação {r50List[0].Situacao}", "Cancelada (101 / 110111)"));
                    }
                    else if (!isCancelled && !sintCancelled)
                    {
                        if (Math.Abs(sintTot - x.ValorTotalCents) > 2)
                        {
                            valueDiffs++;
                            matchStatus = "Divergência de Valor";
                            critical = true;
                            note = $"Diferença de R$ {TaxIdValidator.FormatCentsBrl(sintTot - x.ValorTotalCents)}";
                            findings.Add(new AuditFinding(
                                "AUD-XML-003", Severity.Error, "XML", $"NF-e {x.Numero} Série {x.Serie}",
                                "Divergência no Valor Contábil Total entre o XML da NF-e e o Registro 50.",
                                $"R$ {TaxIdValidator.FormatCentsBrl(sintTot)}",
                                $"R$ {TaxIdValidator.FormatCentsBrl(x.ValorTotalCents)}"));
                        }
                        if (cat == XmlOperationCategory.CompraComercial)
                            xmlComprasLancadas++;
                    }
                    else if (cat == XmlOperationCategory.CompraComercial)
                    {
                        xmlComprasLancadas++;
                    }

                    if (launchedInDifferentMonth)
                    {
                        string crossMonthMsg = $"Lançada no Sintegra em {sintDateBr} (mês {sintMonthLbl}, diferente da emissão XML {dataBr})";
                        note = string.IsNullOrEmpty(note) ? crossMonthMsg : $"{note} | {crossMonthMsg}";
                        findings.Add(new AuditFinding(
                            "AUD-XML-006", Severity.Info, "XML", $"NF-e {x.Numero} Série {x.Serie}",
                            $"NF-e emitida em {dataBr} foi encontrada lançada no Sintegra na competência {sintMonthLbl} ({sintDateBr}).",
                            $"Mês {sintMonthLbl}", $"XML {TaxIdValidator.FormatMonthLabel(ym)}"));
                    }
                    if (matchedWithModelSerieDiff)
                    {
                        string diffModMsg = $"Localizada por CNPJ + Número com Modelo/Série diferente no Reg. 50 (Mod {r50List[0].Modelo} Série '{r50List[0].Serie.Trim()}')";
                        note = string.IsNullOrEmpty(note) ? diffModMsg : $"{note} | {diffModMsg}";
                    }

                    if (ym != "000000" && cat == XmlOperationCategory.CompraComercial)
                    {
                        monthXmlMap.TryGetValue(ym, out var cur);
                        monthXmlMap[ym] = (cur.ComprasTot + 1, cur.ComprasCents + x.ValorTotalCents,
                            cur.Lancadas + 1, cur.Omissas, cur.OmissasCents, cur.OutrasOmissas);
                    }

                    documentRows.Add(new XmlAuditDocumentRow(
                        ym, dataBr, x.EmitCnpj, x.EmitNome, mod, x.Serie, x.Numero,
                        cat, catLabel, matchStatus, critical, x.ValorTotalCents, sintTot,
                        string.Join(", ", x.Cfops), sintCfops, note));
                }
                else
                {
                    returnedByMap.TryGetValue(x.Chave, out var returningXml);
                    bool returnedInFull = returningXml is not null && Math.Abs(returningXml.ValorTotalCents - x.ValorTotalCents) <= 10;

                    string matchStatus;
                    bool isCriticalGap = false;
                    string note;

                    if (isCancelled)
                    {
                        matchStatus = "Cancelada no XML (Não escriturada)";
                        note = "XML cancelado na SEFAZ";
                    }
                    else if (returnedInFull && returningXml is not null)
                    {
                        matchStatus = $"Devolvida (NF {returningXml.Numero})";
                        note = $"Compra devolvida integralmente pela NF de retorno {returningXml.Numero} ({returningXml.NatOp})";
                    }
                    else if (cat == XmlOperationCategory.CompraComercial)
                    {
                        xmlComprasTotal++;
                        xmlComprasOmissas++;
                        xmlComprasOmissasCents += x.ValorTotalCents;
                        missingInSintegra++;
                        isCriticalGap = true;
                        matchStatus = "OMISSA NO SINTEGRA";
                        note = "NF de compra não localizada no Registro 50 do Sintegra";

                        findings.Add(new AuditFinding(
                            "AUD-XML-001", Severity.Error, "XML", $"NF-e {x.Numero} ({x.EmitNome})",
                            $"Compra comercial de {dataBr} não foi lançada no Registro 50 do Sintegra (CFOP XML: {string.Join(", ", x.Cfops)}).",
                            "Ausente", $"R$ {TaxIdValidator.FormatCentsBrl(x.ValorTotalCents)}"));

                        if (ym != "000000")
                        {
                            monthXmlMap.TryGetValue(ym, out var cur);
                            monthXmlMap[ym] = (cur.ComprasTot + 1, cur.ComprasCents + x.ValorTotalCents,
                                cur.Lancadas, cur.Omissas + 1, cur.OmissasCents + x.ValorTotalCents, cur.OutrasOmissas);
                        }
                    }
                    else
                    {
                        xmlOutrasOmissas++;
                        matchStatus = cat == XmlOperationCategory.DevolucaoRetorno
                            ? "Devolução/Retorno (sem Reg 50)"
                            : "Não lançada (Não-compra)";
                        note = returningXml is not null
                            ? $"Retornada pela NF {returningXml.Numero}"
                            : $"{catLabel} sem lançamento no Registro 50";

                        findings.Add(new AuditFinding(
                            "AUD-XML-005", Severity.Warning, "XML", $"NF-e {x.Numero} ({catLabel})",
                            $"{catLabel} presente no pacote XML ({x.EmitNome}, {dataBr}) sem registro no Sintegra.",
                            "Ausente", $"R$ {TaxIdValidator.FormatCentsBrl(x.ValorTotalCents)}"));

                        if (ym != "000000")
                        {
                            monthXmlMap.TryGetValue(ym, out var cur);
                            monthXmlMap[ym] = (cur.ComprasTot, cur.ComprasCents, cur.Lancadas,
                                cur.Omissas, cur.OmissasCents, cur.OutrasOmissas + 1);
                        }
                    }

                    documentRows.Add(new XmlAuditDocumentRow(
                        ym, dataBr, x.EmitCnpj, x.EmitNome, mod, x.Serie, x.Numero,
                        cat, catLabel, matchStatus, isCriticalGap, x.ValorTotalCents, 0,
                        string.Join(", ", x.Cfops), "-", note));
                }
            }
            else if (x.DocKind == "CTe")
            {
                if (sint70ByDocKey.TryGetValue(docKey, out var r70List))
                {
                    matchedDocs++;
                    long sintTot = r70List.Sum(r => r.ValorTotalCents);
                    bool diff = Math.Abs(sintTot - x.ValorTotalCents) > 2;
                    if (diff)
                    {
                        valueDiffs++;
                        findings.Add(new AuditFinding(
                            "AUD-XML-003", Severity.Error, "XML", $"CT-e {x.Numero} Série {x.Serie}",
                            "Divergência no Valor Total do CT-e entre o XML e o Registro 70.",
                            $"R$ {TaxIdValidator.FormatCentsBrl(sintTot)}",
                            $"R$ {TaxIdValidator.FormatCentsBrl(x.ValorTotalCents)}"));
                    }
                    documentRows.Add(new XmlAuditDocumentRow(
                        ym, dataBr, x.EmitCnpj, x.EmitNome, mod, x.Serie, x.Numero,
                        cat, catLabel, diff ? "Divergência de Valor" : "Lançada OK", diff,
                        x.ValorTotalCents, sintTot, string.Join(", ", x.Cfops), r70List[0].Cfop, ""));
                }
                else
                {
                    missingInSintegra++;
                    findings.Add(new AuditFinding(
                        "AUD-XML-001", Severity.Error, "XML", $"CT-e {x.Numero} Série {x.Serie}",
                        "CT-e presente no pacote XML não foi localizado no Registro 70 do Sintegra.",
                        "Ausente", $"R$ {TaxIdValidator.FormatCentsBrl(x.ValorTotalCents)}"));
                    documentRows.Add(new XmlAuditDocumentRow(
                        ym, dataBr, x.EmitCnpj, x.EmitNome, mod, x.Serie, x.Numero,
                        cat, catLabel, "OMISSA NO SINTEGRA", true,
                        x.ValorTotalCents, 0, string.Join(", ", x.Cfops), "-", "CT-e ausente no Registro 70"));
                }
            }
        }

        if (nfeCount > 0)
        {
            var seenUnmatchedR50 = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (fullKey, r50List) in sint50ByFullKey)
            {
                if (!r50List.Any(r => matchedSint50Lines.Contains(r.Line)) && seenUnmatchedR50.Add(fullKey))
                {
                    extraInSintegra++;
                    long tot = r50List.Sum(r => r.ValorTotalCents);
                    var parts = fullKey.Split('|');
                    findings.Add(new AuditFinding(
                        "AUD-XML-002", Severity.Warning, "XML", $"NF {parts[3]} Série {parts[2]} (Mod {parts[1]})",
                        "Nota Fiscal escriturada no Sintegra (Registro 50) sem XML correspondente no lote auditado.",
                        $"R$ {TaxIdValidator.FormatCentsBrl(tot)}", "XML ausente no lote"));
                }
            }
        }

        int spedC100Count = 0;
        if (spedData is not null)
        {
            spedC100Count = spedData.Invoices.Count;
            foreach (var (cfop, val) in spedData.CfopTotals)
                extCfopTotals[cfop] = val;

            foreach (var sinv in spedData.Invoices)
            {
                string docKey = $"{sinv.CodMod}|{NormalizeSerie(sinv.Ser)}|{NormalizeDocNum(sinv.NumDoc)}";
                bool spedCancelled = sinv.CodSit is "02" or "03" or "04" or "05";

                if (sint50ByDocKey.TryGetValue(docKey, out var r50List))
                {
                    matchedDocs++;
                    bool sintCancelled = r50List.All(r => r.Situacao == "S");
                    if (spedCancelled != sintCancelled)
                    {
                        statusDiffs++;
                        findings.Add(new AuditFinding(
                            "AUD-SPD-004", Severity.Error, "SPED", $"NF {sinv.NumDoc} Série {sinv.Ser}",
                            "Divergência de situação da Nota Fiscal entre SPED (C100 COD_SIT) e Sintegra (Reg 50).",
                            $"Situação {r50List[0].Situacao}", $"COD_SIT {sinv.CodSit}"));
                    }
                    else if (!spedCancelled)
                    {
                        long sintTot = r50List.Sum(r => r.ValorTotalCents);
                        if (Math.Abs(sintTot - sinv.VlDocCents) > 2)
                        {
                            valueDiffs++;
                            findings.Add(new AuditFinding(
                                "AUD-SPD-003", Severity.Error, "SPED", $"NF {sinv.NumDoc} Série {sinv.Ser}",
                                "Divergência no Valor Contábil entre SPED C100 e Sintegra Registro 50.",
                                $"R$ {TaxIdValidator.FormatCentsBrl(sintTot)}",
                                $"R$ {TaxIdValidator.FormatCentsBrl(sinv.VlDocCents)}"));
                        }
                    }
                }
                else if (sinv.CodMod is "55" or "01")
                {
                    missingInSintegra++;
                    findings.Add(new AuditFinding(
                        "AUD-SPD-001", Severity.Error, "SPED", $"NF {sinv.NumDoc} Série {sinv.Ser} (C100)",
                        "Nota Fiscal escriturada no SPED (C100) ausente no Registro 50 do Sintegra.",
                        "Ausente", $"R$ {TaxIdValidator.FormatCentsBrl(sinv.VlDocCents)}"));
                }
            }
        }

        // Build Monthly Coverage Table combining Sintegra MonthlyBalance + XML Coverage
        var balByYm = sintReport.MonthlyBalance.ToDictionary(m => m.YearMonth, StringComparer.Ordinal);
        var coverageRows = new List<MonthlyXmlCoverageRow>(monthXmlMap.Count);
        foreach (var (ym, xc) in monthXmlMap)
        {
            if (ym == "000000") continue;
            balByYm.TryGetValue(ym, out var bal);
            coverageRows.Add(new MonthlyXmlCoverageRow(
                ym,
                TaxIdValidator.FormatMonthLabel(ym),
                xc.ComprasTot,
                xc.ComprasCents,
                xc.Lancadas,
                xc.Omissas,
                xc.OmissasCents,
                xc.OutrasOmissas,
                bal?.TotalEntradasCents ?? 0,
                bal?.TotalSaidasCents ?? 0));
        }

        var cfopComp = new List<CfopComparisonRow>();
        if (extCfopTotals.Count > 0)
        {
            var sintCfop = new SortedDictionary<string, (long Contabil, long Icms)>(StringComparer.Ordinal);
            foreach (var r50 in sintegra.Reg50.Where(r => r.Situacao != "S"))
            {
                sintCfop.TryGetValue(r50.Cfop, out var cur);
                sintCfop[r50.Cfop] = (cur.Contabil + r50.ValorTotalCents, cur.Icms + r50.ValorIcmsCents);
            }
            var allCfops = sintCfop.Keys.Union(extCfopTotals.Keys, StringComparer.Ordinal).OrderBy(x => x);
            foreach (string cfop in allCfops)
            {
                sintCfop.TryGetValue(cfop, out var sc);
                extCfopTotals.TryGetValue(cfop, out var ec);
                cfopComp.Add(new CfopComparisonRow(
                    cfop, sc.Contabil, ec.Contabil, sc.Contabil - ec.Contabil,
                    sc.Icms, ec.Icms, sc.Icms - ec.Icms));
            }
        }

        int totalExternal = nfeCount + cteCount + spedC100Count;
        int totalIssues = missingInSintegra + valueDiffs + statusDiffs;
        double score = totalExternal == 0
            ? 100.0
            : Math.Round((Math.Max(0, totalExternal - totalIssues) / (double)totalExternal) * 100.0, 2);

        var sortedDocs = documentRows
            .OrderByDescending(r => r.IsCriticalGap)
            .ThenBy(r => r.YearMonth, StringComparer.Ordinal)
            .ThenBy(r => r.Numero, StringComparer.Ordinal)
            .ToList();

        return new AuditReport(
            score, totalExternal, matchedDocs, missingInSintegra, extraInSintegra,
            valueDiffs, statusDiffs, nfeCount, nfceCount, cteCount, nfseCount,
            cancelEventsCount, spedC100Count, xmlComprasTotal, xmlComprasLancadas,
            xmlComprasOmissas, xmlComprasOmissasCents, xmlOutrasOmissas,
            findings, sortedDocs, coverageRows, cfopComp);
    }
}
