using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace OpenSintegra.Core;

public sealed record XmlInjectionRequest(
    XmlDocumentSummary Xml,
    string EntryCfop,
    string? EntryDateYyyyMmDd = null
);

public sealed record XmlInjectionResult(
    int InjectedInvoicesCount,
    int SkippedDuplicateCount,
    int GeneratedReg54Items,
    int GeneratedReg75Products,
    FixReport FixReport,
    ValidationReport AfterReport,
    byte[] UpdatedBytes,
    int UpdatedExistingCount = 0
);

public sealed record XmlCopyExportResult(
    int ExportedCount,
    string OutputDirectory,
    IReadOnlyList<string> Warnings
);

/// <summary>
/// Converts omitted purchase NF-e XML documents directly into compliant 126-column Sintegra
/// records (50, 54, 75), supports reprocessing existing Reg. 50 entries with custom Entry Dates/CFOPs,
/// exports adjusted XML working copies to a separate subfolder, and rebuilds Record 90 via AutoFix.
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
        bool includeReg54Items = true) =>
        ApplyXmlRequests(sintegraBytes, requests, updateExistingEntries: false, simplesNacionalZeroIcms, includeReg54Items);

    public static XmlInjectionResult ReprocessXmlInvoices(
        byte[] sintegraBytes,
        IReadOnlyList<XmlInjectionRequest> requests,
        bool simplesNacionalZeroIcms = true,
        bool includeReg54Items = true) =>
        ApplyXmlRequests(sintegraBytes, requests, updateExistingEntries: true, simplesNacionalZeroIcms, includeReg54Items);

    private static XmlInjectionResult ApplyXmlRequests(
        byte[] sintegraBytes,
        IReadOnlyList<XmlInjectionRequest> requests,
        bool updateExistingEntries,
        bool simplesNacionalZeroIcms,
        bool includeReg54Items)
    {
        var doc = SintegraParser.ParseBytes(sintegraBytes);
        string buyerUf = doc.Reg10?.Uf.Trim() ?? "";
        string r10Di = doc.Reg10?.DataInicial ?? "20260101";
        string r10Df = doc.Reg10?.DataFinal ?? "20261231";

        string[] lineTexts = new string[doc.Lines.Count + 1];
        foreach (var l in doc.Lines)
            lineTexts[l.LineNumber] = l.RawText;

        var reg50ByFullKey = new Dictionary<string, List<Reg50>>(StringComparer.Ordinal);
        var reg50ByCnpjNum = new Dictionary<string, List<Reg50>>(StringComparer.Ordinal);
        foreach (var r50 in doc.Reg50)
        {
            string fullKey = BuildInvoiceFullKey(r50.Cnpj, r50.Modelo, r50.Serie, r50.Numero);
            AddToMap(reg50ByFullKey, fullKey, r50);
            string cnpjNum = BuildCnpjNumKey(r50.Cnpj, r50.Numero);
            AddToMap(reg50ByCnpjNum, cnpjNum, r50);
        }

        var reg54ByFullKey = new Dictionary<string, List<Reg54>>(StringComparer.Ordinal);
        var reg54ByCnpjNum = new Dictionary<string, List<Reg54>>(StringComparer.Ordinal);
        foreach (var r54 in doc.Reg54)
        {
            string fullKey = BuildInvoiceFullKey(r54.Cnpj, r54.Modelo, r54.Serie, r54.Numero);
            AddToMap(reg54ByFullKey, fullKey, r54);
            string cnpjNum = BuildCnpjNumKey(r54.Cnpj, r54.Numero);
            AddToMap(reg54ByCnpjNum, cnpjNum, r54);
        }

        var existingProdCodes = new HashSet<string>(doc.Reg75.Select(x => x.CodProduto), StringComparer.Ordinal);
        var processedBatchKeys = new HashSet<string>(StringComparer.Ordinal);
        var addedLines = new List<string>();

        int injectedCount = 0;
        int updatedExistingCount = 0;
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
            string fullKey = $"{cnpj}|{mod}|{NormSer(x.Serie)}|{numMod1M}";
            string cnpjNumKey = $"{cnpj}|{numMod1M}";

            if (!processedBatchKeys.Add(fullKey))
            {
                skippedCount++;
                continue;
            }

            List<Reg50>? existingList = null;
            if (!reg50ByFullKey.TryGetValue(fullKey, out existingList) && updateExistingEntries)
                reg50ByCnpjNum.TryGetValue(cnpjNumKey, out existingList);

            if (existingList is { Count: > 0 })
            {
                if (!updateExistingEntries)
                {
                    skippedCount++;
                    continue;
                }

                string? requestedDate = TaxIdValidator.ParseFlexibleDateYyyyMmDd(req.EntryDateYyyyMmDd);
                string distinctCfop = existingList.Select(r => r.Cfop.Trim()).Distinct(StringComparer.Ordinal).Count() == 1
                    ? existingList[0].Cfop.Trim()
                    : "";
                string? requestedCfop = !string.IsNullOrEmpty(distinctCfop) && TaxIdValidator.ValidateCfop(req.EntryCfop)
                    ? req.EntryCfop.Trim()
                    : null;

                bool invoiceChanged = false;
                foreach (var r50 in existingList)
                {
                    string raw50 = lineTexts[r50.Line];
                    if (raw50.Length < 56 || !raw50.StartsWith("50", StringComparison.Ordinal))
                        continue;

                    bool lineChanged = false;
                    if (requestedDate is not null && raw50.Substring(30, 8) != requestedDate)
                    {
                        raw50 = raw50[..30] + requestedDate + raw50[38..];
                        lineChanged = true;
                    }

                    string origCfop = raw50.Substring(51, 4);
                    if (requestedCfop is not null && origCfop != requestedCfop)
                    {
                        raw50 = raw50[..51] + requestedCfop + raw50[55..];
                        lineChanged = true;

                        string r50Key = BuildInvoiceFullKey(r50.Cnpj, r50.Modelo, r50.Serie, r50.Numero);
                        if (!reg54ByFullKey.TryGetValue(r50Key, out var r54List))
                            reg54ByCnpjNum.TryGetValue(BuildCnpjNumKey(r50.Cnpj, r50.Numero), out r54List);

                        if (r54List is not null)
                        {
                            foreach (var r54 in r54List)
                            {
                                string raw54 = lineTexts[r54.Line];
                                if (raw54.Length >= 31 && raw54.StartsWith("54", StringComparison.Ordinal) &&
                                    raw54.Substring(27, 4) == origCfop)
                                {
                                    lineTexts[r54.Line] = raw54[..27] + requestedCfop + raw54[31..];
                                }
                            }
                        }
                    }

                    if (lineChanged)
                    {
                        lineTexts[r50.Line] = raw50;
                        invoiceChanged = true;
                    }
                }

                if (invoiceChanged)
                    updatedExistingCount++;
                else
                    skippedCount++;

                continue;
            }

            string dataYmd = TaxIdValidator.ParseFlexibleDateYyyyMmDd(req.EntryDateYyyyMmDd)
                ?? (TaxIdValidator.ValidateDateYyyyMmDd(x.DataEmissao) ? x.DataEmissao : r10Di);

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

        // Preserve non-90 lines (including any updated Reg 50/54) + newly injected lines; AutoFix rebuilds Reg 90
        var finalRawLines = new List<string>(doc.Lines.Count + addedLines.Count);
        foreach (var l in doc.Lines)
        {
            if (l.RecordType != "90")
                finalRawLines.Add(lineTexts[l.LineNumber]);
        }
        finalRawLines.AddRange(addedLines);

        byte[] mergedBytes = Encoding.Latin1.GetBytes(string.Join("\r\n", finalRawLines) + "\r\n");
        var fx = SintegraAutoFixer.AutoFix(mergedBytes, new FixOptions());
        var afterDoc = SintegraParser.ParseBytes(fx.FixedBytes);
        var afterRep = SintegraValidator.Validate(afterDoc);

        return new XmlInjectionResult(injectedCount, skippedCount, reg54Count, reg75Count, fx, afterRep, fx.FixedBytes, updatedExistingCount);
    }

    /// <summary>
    /// Updates dhSaiEnt (or legacy dSaiEnt) in working copies of NF-e XML files without touching original files.
    /// Note: Modifying XML elements inside infNFe alters its content relative to the SEFAZ XML signature,
    /// so copies are written exclusively to a separate output folder.
    /// </summary>
    public static string PatchXmlEntryDateContent(string xmlContent, string targetYyyyMmDd)
    {
        if (!TaxIdValidator.ValidateDateYyyyMmDd(targetYyyyMmDd))
            return xmlContent;

        string isoDate = $"{targetYyyyMmDd[0..4]}-{targetYyyyMmDd[4..6]}-{targetYyyyMmDd[6..8]}";
        var xdoc = XDocument.Parse(xmlContent.TrimStart('\uFEFF'), LoadOptions.PreserveWhitespace);
        var root = xdoc.Root;
        if (root is null) return xmlContent;

        var infNfe = root.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("infNFe", StringComparison.OrdinalIgnoreCase));
        var ide = infNfe?.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("ide", StringComparison.OrdinalIgnoreCase));
        if (ide is null) return xmlContent;

        var dhSaiEntEl = ide.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("dhSaiEnt", StringComparison.OrdinalIgnoreCase));
        var dSaiEntEl = ide.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("dSaiEnt", StringComparison.OrdinalIgnoreCase));
        var dhEmiEl = ide.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("dhEmi", StringComparison.OrdinalIgnoreCase));
        var dEmiEl = ide.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("dEmi", StringComparison.OrdinalIgnoreCase));

        if (dhSaiEntEl is not null)
        {
            string cur = dhSaiEntEl.Value.Trim();
            string suffix = cur.Length > 10 ? cur[10..] : "T00:00:00-03:00";
            dhSaiEntEl.Value = isoDate + suffix;
        }
        else if (dSaiEntEl is not null)
        {
            dSaiEntEl.Value = isoDate;
        }
        else if (dhEmiEl is not null)
        {
            string emi = dhEmiEl.Value.Trim();
            string suffix = emi.Length > 10 ? emi[10..] : "T00:00:00-03:00";
            dhEmiEl.AddAfterSelf(new XElement(ide.Name.Namespace + "dhSaiEnt", isoDate + suffix));
        }
        else if (dEmiEl is not null)
        {
            dEmiEl.AddAfterSelf(new XElement(ide.Name.Namespace + "dSaiEnt", isoDate));
        }
        else
        {
            ide.Add(new XElement(ide.Name.Namespace + "dhSaiEnt", isoDate + "T00:00:00-03:00"));
        }

        using var sw = new Utf8StringWriter();
        xdoc.Save(sw, SaveOptions.DisableFormatting);
        return sw.ToString();
    }

    public static XmlCopyExportResult ExportAdjustedXmlCopies(
        IReadOnlyList<string> xmlSourcePaths,
        string outputDirectory,
        IReadOnlyList<XmlInjectionRequest> requests)
    {
        var dateByChave = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dateByFullKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var dateByCnpjNum = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var req in requests)
        {
            string? targetDate = TaxIdValidator.ParseFlexibleDateYyyyMmDd(req.EntryDateYyyyMmDd)
                ?? TaxIdValidator.ParseFlexibleDateYyyyMmDd(req.Xml.DataSaiEnt)
                ?? TaxIdValidator.ParseFlexibleDateYyyyMmDd(req.Xml.DataEmissao);
            if (targetDate is null) continue;

            if (!string.IsNullOrWhiteSpace(req.Xml.Chave))
                dateByChave.TryAdd(req.Xml.Chave.Trim(), targetDate);

            string cnpj = DigitsOnly(req.Xml.EmitCnpj, 14);
            string mod = req.Xml.Modelo.Trim().PadLeft(2, '0');
            if (mod.Length > 2) mod = mod[^2..];
            dateByFullKey.TryAdd(BuildInvoiceFullKey(cnpj, mod, req.Xml.Serie, req.Xml.Numero), targetDate);
            dateByCnpjNum.TryAdd(BuildCnpjNumKey(cnpj, req.Xml.Numero), targetDate);
        }

        if (dateByChave.Count == 0 && dateByFullKey.Count == 0)
            return new XmlCopyExportResult(0, outputDirectory, Array.Empty<string>());

        Directory.CreateDirectory(outputDirectory);
        int exported = 0;
        var exportedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        void TryProcessRawXml(string rawEntryName, string xmlText)
        {
            var summary = FiscalCrossAuditor.ParseFiscalXml(rawEntryName, xmlText);
            if (summary is null || summary.DocKind != "NFe") return;

            string? targetDate = null;
            if (!string.IsNullOrWhiteSpace(summary.Chave))
                dateByChave.TryGetValue(summary.Chave.Trim(), out targetDate);

            string cnpj = DigitsOnly(summary.EmitCnpj, 14);
            string mod = summary.Modelo.Trim().PadLeft(2, '0');
            if (mod.Length > 2) mod = mod[^2..];
            string fullKey = BuildInvoiceFullKey(cnpj, mod, summary.Serie, summary.Numero);
            string cnpjNumKey = BuildCnpjNumKey(cnpj, summary.Numero);

            if (targetDate is null && !dateByFullKey.TryGetValue(fullKey, out targetDate))
                dateByCnpjNum.TryGetValue(cnpjNumKey, out targetDate);

            if (targetDate is null) return;

            string dedupeId = !string.IsNullOrWhiteSpace(summary.Chave) ? summary.Chave.Trim() : fullKey;
            if (!exportedKeys.Add(dedupeId)) return;

            string patched = PatchXmlEntryDateContent(xmlText, targetDate);
            string baseName = Path.GetFileName(rawEntryName);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = $"{(string.IsNullOrWhiteSpace(summary.Chave) ? fullKey.Replace('|', '_') : summary.Chave)}.xml";

            string candidate = baseName;
            int suffix = 1;
            while (!usedFileNames.Add(candidate))
                candidate = $"{Path.GetFileNameWithoutExtension(baseName)}_{++suffix}{Path.GetExtension(baseName)}";

            File.WriteAllText(Path.Combine(outputDirectory, candidate), patched, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            exported++;
        }

        void ScanPath(string p)
        {
            if (Directory.Exists(p))
            {
                foreach (var f in Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories))
                {
                    if (FiscalCrossAuditor.IsInsideAdjustedCopiesFolder(f, p))
                        continue;
                    if (f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        ScanPath(f);
                }
                return;
            }
            if (!File.Exists(p)) return;

            if (p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(p);
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
                        using var sr = new StreamReader(entry.Open());
                        TryProcessRawXml(entry.Name, sr.ReadToEnd());
                    }
                }
                catch (Exception ex) { warnings.Add($"{Path.GetFileName(p)}: {ex.Message}"); }
            }
            else if (p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                try { TryProcessRawXml(Path.GetFileName(p), File.ReadAllText(p)); }
                catch (Exception ex) { warnings.Add($"{Path.GetFileName(p)}: {ex.Message}"); }
            }
        }

        foreach (string src in xmlSourcePaths.Where(s => !string.IsNullOrWhiteSpace(s)))
            ScanPath(src);

        return new XmlCopyExportResult(exported, outputDirectory, warnings);
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }

    private static string BuildInvoiceFullKey(string cnpj, string modelo, string serie, string numero) =>
        $"{DigitsOnly(cnpj, 14)}|{modelo.Trim().PadLeft(2, '0')[^2..]}|{NormSer(serie)}|{NormNum(numero)}";

    private static string BuildCnpjNumKey(string cnpj, string numero) =>
        $"{DigitsOnly(cnpj, 14)}|{NormNum(numero)}";

    private static void AddToMap<T>(Dictionary<string, List<T>> map, string key, T item)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<T>();
            map[key] = list;
        }
        list.Add(item);
    }

    private static string NormSer(string s)
    {
        string t = s.Trim();
        if (string.IsNullOrEmpty(t)) return "0";
        return t.All(char.IsAsciiDigit) && int.TryParse(t, out int n) ? n.ToString() : t.ToUpperInvariant();
    }

    private static long NormNum(string n)
    {
        var digits = new string(n.Where(char.IsAsciiDigit).ToArray());
        return long.TryParse(digits, out long p) ? p % 1_000_000L : 0L;
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
