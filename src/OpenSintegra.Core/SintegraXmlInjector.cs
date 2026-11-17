using System.Globalization;
using System.Text;

namespace OpenSintegra.Core;

public sealed record XmlInjectionRequest(
    XmlDocumentSummary Xml,
    string EntryCfop
);

public sealed record XmlInjectionResult(
    int InjectedInvoicesCount,
    int SkippedDuplicateCount,
    int GeneratedReg54Items,
    int GeneratedReg75Products,
    FixReport FixReport,
    ValidationReport AfterReport,
    byte[] UpdatedBytes
);

/// <summary>
/// Converts omitted purchase NF-e XML documents directly into compliant 126-column Sintegra
/// records (50, 54, 75) and rebuilds Record 90 totals cleanly via SintegraAutoFixer.
/// </summary>
public static class SintegraXmlInjector
{
    public static string SuggestEntryCfop(string? supplierCfop, string? supplierUf, string? buyerUf)
    {
        bool sameState = string.IsNullOrWhiteSpace(supplierUf) ||
                         string.IsNullOrWhiteSpace(buyerUf) ||
                         string.Equals(supplierUf.Trim(), buyerUf.Trim(), StringComparison.OrdinalIgnoreCase);

        string c = (supplierCfop ?? "").Trim();
        char lead = sameState ? '1' : '2';
        if (c.Length == 4 && c.All(char.IsAsciiDigit))
        {
            string tail = c[1..];
            return tail switch
            {
                "401" or "403" or "404" or "405" => $"{lead}403",
                "101" or "102" or "103" or "104" or "105" or "106" => $"{lead}102",
                "655" or "656" => $"{lead}652",
                "910" or "911" => $"{lead}910",
                "908" => $"{lead}908",
                _ => $"{lead}{tail}"
            };
        }
        return $"{lead}102";
    }

    public static XmlInjectionResult InjectXmlInvoices(
        byte[] sintegraBytes,
        IReadOnlyList<XmlInjectionRequest> requests,
        bool simplesNacionalZeroIcms = true,
        bool includeReg54Items = true)
    {
        var doc = SintegraParser.ParseBytes(sintegraBytes);
        string buyerUf = doc.Reg10?.Uf.Trim() ?? "";
        string r10Di = doc.Reg10?.DataInicial ?? "20260101";
        string r10Df = doc.Reg10?.DataFinal ?? "20261231";

        // Track existing invoices (CNPJ|Mod|Ser|NumMod1M) to prevent duplicates
        static string NormSer(string s)
        {
            string t = s.Trim();
            if (string.IsNullOrEmpty(t)) return "0";
            return t.All(char.IsAsciiDigit) && int.TryParse(t, out int n) ? n.ToString() : t.ToUpperInvariant();
        }
        static long NormNum(string n)
        {
            var digits = new string(n.Where(char.IsAsciiDigit).ToArray());
            return long.TryParse(digits, out long p) ? p % 1_000_000L : 0L;
        }

        var existingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r50 in doc.Reg50)
            existingKeys.Add($"{r50.Cnpj.Trim()}|{r50.Modelo.Trim()}|{NormSer(r50.Serie)}|{NormNum(r50.Numero)}");

        var existingProdCodes = new HashSet<string>(doc.Reg75.Select(x => x.CodProduto), StringComparer.Ordinal);

        var addedLines = new List<string>();
        int injectedCount = 0;
        int skippedCount = 0;
        int reg54Count = 0;
        int reg75Count = 0;

        foreach (var req in requests)
        {
            var x = req.Xml;
            string cnpj = DigitsOnly(x.EmitCnpj, 14);
            if (!TaxIdValidator.ValidateCnpjOrCpf(cnpj)) continue;

            string mod = x.Modelo.Trim().PadLeft(2, '0');
            if (mod.Length > 2) mod = mod[^2..];
            long numMod1M = NormNum(x.Numero);
            string key = $"{cnpj}|{mod}|{NormSer(x.Serie)}|{numMod1M}";
            if (!existingKeys.Add(key))
            {
                skippedCount++;
                continue;
            }

            string dataYmd = TaxIdValidator.ValidateDateYyyyMmDd(x.DataEmissao) ? x.DataEmissao : r10Di;
            string uf = !string.IsNullOrWhiteSpace(x.FullData?.SupplierUf) && TaxIdValidator.IsValidUf(x.FullData!.SupplierUf)
                ? x.FullData.SupplierUf.Trim().ToUpperInvariant()
                : (string.IsNullOrWhiteSpace(buyerUf) ? "MG" : buyerUf);

            string rawIe = ToAsciiUpper(x.FullData?.SupplierIe ?? "ISENTO");
            rawIe = rawIe.Contains("ISENTO", StringComparison.Ordinal)
                ? "ISENTO"
                : new string(rawIe.Where(char.IsAsciiLetterOrDigit).ToArray());
            if (string.IsNullOrWhiteSpace(rawIe)) rawIe = "ISENTO";
            string ieField = FitRight(rawIe, 14);

            string rawSer = ToAsciiUpper(x.Serie.TrimStart('0'));
            if (string.IsNullOrEmpty(rawSer)) rawSer = "1";
            string serField = FitRight(rawSer, 3);
            string numField = $"{numMod1M:000000}";

            string cfop = TaxIdValidator.ValidateCfop(req.EntryCfop)
                ? req.EntryCfop.Trim()
                : SuggestEntryCfop(x.Cfops.FirstOrDefault(), uf, buyerUf);

            long vTot = Math.Max(0, x.ValorTotalCents);
            long vBc = simplesNacionalZeroIcms ? 0 : Math.Max(0, x.BaseIcmsCents);
            long vIcms = simplesNacionalZeroIcms ? 0 : Math.Max(0, x.ValorIcmsCents);
            long aliqBps = (vBc > 0 && vIcms > 0) ? Math.Clamp((long)Math.Round(vIcms * 10000.0 / vBc), 0L, 9999L) : 0L;
            long vOutras = cfop.EndsWith("910", StringComparison.Ordinal) ? vTot : 0L;

            string line50 =
                $"50{cnpj}{ieField}{dataYmd}{FitRight(uf, 2)}{mod}{serField}{numField}{cfop}T" +
                $"{vTot:0000000000000}{vBc:0000000000000}{vIcms:0000000000000}{0:0000000000000}{vOutras:0000000000000}{aliqBps:0000}N";
            addedLines.Add(line50);
            injectedCount++;

            if (includeReg54Items && x.FullData?.Items is { Count: > 0 } xmlItems)
            {
                long itemsSum = 0;
                int itemSeq = 0;
                foreach (var it in xmlItems)
                {
                    itemSeq++;
                    if (itemSeq > 990) break;

                    string prodCode = MakeSafeProdCode(it.CodProduto, itemSeq);
                    string cst = DigitsOnly(it.CstCsosn, 3);
                    if (cst.Length == 2) cst = "0" + cst;
                    if (cst.Length != 3) cst = "000";

                    long qtyMilli = Math.Clamp(it.QuantidadeMilli > 0 ? it.QuantidadeMilli : 1000L, 0L, 99_999_999_999L);
                    long bruto = Math.Max(0, it.ValorBrutoCents);
                    long desc = Math.Max(0, it.DescontoCents);
                    long itemBc = simplesNacionalZeroIcms ? 0 : Math.Max(0, it.BaseIcmsCents);
                    long itemIpi = simplesNacionalZeroIcms ? 0 : Math.Max(0, it.ValorIpiCents);
                    long itemAliq = simplesNacionalZeroIcms ? 0 : Math.Clamp(it.AliquotaIcmsBps, 0L, 9999L);

                    itemsSum += bruto - desc + itemIpi;

                    string line54 =
                        $"54{cnpj}{mod}{serField}{numField}{cfop}{cst}{itemSeq:000}" +
                        $"{FitRight(prodCode, 14)}{qtyMilli:00000000000}{bruto:000000000000}{desc:000000000000}" +
                        $"{itemBc:000000000000}{0:000000000000}{itemIpi:000000000000}{itemAliq:0000}";
                    addedLines.Add(line54);
                    reg54Count++;

                    if (existingProdCodes.Add(prodCode))
                    {
                        string ncm = DigitsOnly(it.Ncm, 8);
                        string descText = FitRight(ToAsciiUpper(it.Descricao), 53);
                        string unid = FitRight(ToAsciiUpper(string.IsNullOrWhiteSpace(it.Unidade) ? "UN" : it.Unidade), 6);
                        string line75 =
                            $"75{r10Di}{r10Df}{FitRight(prodCode, 14)}{ncm}{descText}{unid}00000{itemAliq:0000}00000{0:0000000000000}";
                        addedLines.Add(line75);
                        reg75Count++;
                    }
                }

                // Reconcile freight/ST/other expenses so Record 50 total matches Record 54 item sum exactly
                long delta = vTot - itemsSum;
                if (delta > 0)
                {
                    string accNum = x.FullData.FreteCents > 0 ? "991" : "999";
                    string lineAcc =
                        $"54{cnpj}{mod}{serField}{numField}{cfop}000{accNum}" +
                        $"{new string(' ', 14)}{0:00000000000}{0:000000000000}{delta:000000000000}" +
                        $"{0:000000000000}{0:000000000000}{0:000000000000}0000";
                    addedLines.Add(lineAcc);
                    reg54Count++;
                }
            }
        }

        // Merge raw existing lines (dropping trailing 90; AutoFix rebuilds Reg 90 & sorts cleanly)
        var rawLines = doc.Lines
            .Where(l => l.RecordType != "90")
            .Select(l => l.RawText)
            .ToList();
        rawLines.AddRange(addedLines);

        byte[] mergedBytes = Encoding.Latin1.GetBytes(string.Join("\r\n", rawLines) + "\r\n");
        var fx = SintegraAutoFixer.AutoFix(mergedBytes, new FixOptions());
        var afterDoc = SintegraParser.ParseBytes(fx.FixedBytes);
        var afterRep = SintegraValidator.Validate(afterDoc);

        return new XmlInjectionResult(injectedCount, skippedCount, reg54Count, reg75Count, fx, afterRep, fx.FixedBytes);
    }

    private static string MakeSafeProdCode(string? raw, int seq)
    {
        string clean = ToAsciiUpper(raw).Trim();
        if (string.IsNullOrEmpty(clean)) clean = $"ITEM{seq}";
        return clean.Length <= 14 ? clean : clean[..14];
    }

    private static string FitRight(string s, int len)
    {
        if (s.Length == len) return s;
        return s.Length < len ? s.PadRight(len, ' ') : s[..len];
    }

    private static string DigitsOnly(string? s, int len)
    {
        string d = new((s ?? "").Where(char.IsAsciiDigit).ToArray());
        if (d.Length == len) return d;
        return d.Length < len ? d.PadLeft(len, '0') : d[^len..];
    }

    private static string ToAsciiUpper(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        string formD = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (char ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && ch <= 126)
                sb.Append(char.ToUpperInvariant(ch));
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
