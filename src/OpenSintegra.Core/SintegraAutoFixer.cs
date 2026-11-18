using System.Text;

namespace OpenSintegra.Core;

public sealed class FixOptions
{
    public bool NormalizeCrlf { get; set; } = true;
    public bool NormalizeLength126 { get; set; } = true;
    public bool RemoveOrphan75 { get; set; } = true;
    public bool SynthesizeMissing75 { get; set; } = true;
    public bool Align75Dates { get; set; } = true;
    public bool Deduplicate75 { get; set; } = true;
    public bool ZeroCancelled50 { get; set; } = true;
    public bool RebuildReg90AndSort { get; set; } = true;
}

public sealed record AppliedFix(string Code, string Description, int AffectedCount);

public sealed record FixReport(
    IReadOnlyList<AppliedFix> AppliedFixes,
    ValidationSummary BeforeSummary,
    ValidationSummary AfterSummary,
    string FixedContent,
    byte[] FixedBytes
);

public sealed record MonthlySplitResult(
    string YearMonth,
    string MonthLabel,
    string SuggestedFileName,
    int LineCount,
    byte[] FileBytes,
    ValidationSummary Summary
);

public static class SintegraAutoFixer
{
    public static bool CanSafelyNormalizeLength(string recType, string rawText)
    {
        if (rawText.Length == 126) return true;
        if (rawText.Length > 126)
            return string.IsNullOrWhiteSpace(rawText[126..]);

        // Trailing blank-padded record types can be safely padded to 126 columns.
        // Fixed numeric/flag suffixes (50/51/54/70/76) cannot safely be space-padded if digits are missing.
        return recType is "10" or "11" or "53" or "55" or "56" or "60" or "61" or "74" or "75" or "77" or "85" or "86" or "88";
    }

    private static string SafeNormalize126(string recType, string s, out bool changed)
    {
        changed = false;
        if (s.Length == 126) return s;
        if (s.Length > 126 && string.IsNullOrWhiteSpace(s[126..]))
        {
            changed = true;
            return s[..126];
        }
        if (s.Length < 126 && CanSafelyNormalizeLength(recType, s))
        {
            changed = true;
            return s.PadRight(126, ' ');
        }
        return s;
    }

    private static string PadField(string s, int len)
    {
        if (s.Length == len) return s;
        return s.Length < len ? s.PadRight(len, ' ') : s[..len];
    }

    private static string PadOrTrimExact(string s)
    {
        if (s.Length == 126) return s;
        return s.Length < 126 ? s.PadRight(126, ' ') : s[..126];
    }

    private static int RecordSortRank(string recType, string? subtype) => recType switch
    {
        "10" => 10,
        "11" => 11,
        "50" => 50,
        "51" => 51,
        "53" => 53,
        "54" => 54,
        "55" => 55,
        "56" => 56,
        "57" => 57,
        "60" => subtype switch
        {
            "M" => 600,
            "A" => 601,
            "D" => 602,
            "I" => 603,
            "R" => 604,
            _ => 605
        },
        "61" => subtype == "R" ? 611 : 610,
        "70" => 700,
        "71" => 710,
        "74" => 740,
        "75" => 750,
        "76" => 760,
        "77" => 770,
        "85" => 850,
        "86" => 860,
        "88" => 880,
        "90" => 900,
        _ => 890
    };

    private static Dictionary<string, long> CollectReferencedProducts(SintegraDocument doc)
    {
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var r54 in doc.Reg54)
        {
            if (r54.NumItem is not ("991" or "992" or "999") && !string.IsNullOrEmpty(r54.CodProduto))
                map.TryAdd(r54.CodProduto, r54.AliquotaBps);
        }
        foreach (var r60d in doc.Reg60D)
            if (!string.IsNullOrEmpty(r60d.CodProduto)) map.TryAdd(r60d.CodProduto, 0L);
        foreach (var r60i in doc.Reg60I)
            if (!string.IsNullOrEmpty(r60i.CodProduto)) map.TryAdd(r60i.CodProduto, 0L);
        foreach (var r60r in doc.Reg60R)
            if (!string.IsNullOrEmpty(r60r.CodProduto)) map.TryAdd(r60r.CodProduto, r60r.AliquotaBps);
        foreach (var r61r in doc.Reg61R)
            if (!string.IsNullOrEmpty(r61r.CodProduto)) map.TryAdd(r61r.CodProduto, r61r.AliquotaBps);
        foreach (var r74 in doc.Reg74)
            if (!string.IsNullOrEmpty(r74.CodProduto)) map.TryAdd(r74.CodProduto, 0L);
        foreach (var code in doc.OtherProductCodes)
            if (!string.IsNullOrEmpty(code)) map.TryAdd(code, 0L);
        return map;
    }

    public static FixReport AutoFix(byte[] rawBytes, FixOptions options)
    {
        var initialDoc = SintegraParser.ParseBytes(rawBytes);
        var beforeRep = SintegraValidator.Validate(initialDoc);
        var appliedFixes = new List<AppliedFix>();

        int crlfFixed = 0, lenFixed = 0, cancelledFixed = 0;
        int removedOrphan75 = 0, deduped75 = 0, aligned75Dates = 0, synthesized75 = 0;

        string r10Cnpj = PadField(initialDoc.Reg10?.Cnpj ?? "00000000000000", 14);
        string r10Ie = PadField(initialDoc.Reg10?.Ie ?? "ISENTO", 14);
        string r10Di = PadField(initialDoc.Reg10?.DataInicial ?? "20260101", 8);
        string r10Df = PadField(initialDoc.Reg10?.DataFinal ?? "20260131", 8);
        bool isMultiMonth = r10Di.Length == 8 && r10Df.Length == 8 && r10Di[..6] != r10Df[..6];

        var referencedProds = CollectReferencedProducts(initialDoc);
        var workingLines = new List<(string RecType, string? Subtype, string Text)>();
        var seen75Codes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in initialDoc.Lines)
        {
            if ((!line.HasCr || !line.HasLf) && options.NormalizeCrlf)
                crlfFixed++;

            string text = line.RawText;
            if (text.Length != 126 && options.NormalizeLength126)
            {
                text = SafeNormalize126(line.RecordType, text, out bool didFixLen);
                if (didFixLen) lenFixed++;
            }

            string recType = line.RecordType;
            if (recType == "90" && options.RebuildReg90AndSort)
                continue;

            if (recType == "50" && options.ZeroCancelled50 && text.Length == 126 && text[125] == 'S')
            {
                string valSlice = SintegraParser.SliceCols(text, 57, 125);
                if (valSlice.Any(c => c != '0'))
                {
                    text = text[..56] + new string('0', 69) + "S";
                    cancelledFixed++;
                }
            }

            if (recType == "75" && text.Length >= 32)
            {
                string prodCode = SintegraParser.SliceCols(text, 19, 32).Trim();
                if (options.RemoveOrphan75 && !referencedProds.ContainsKey(prodCode))
                {
                    removedOrphan75++;
                    continue;
                }
                if (options.Deduplicate75 && !seen75Codes.Add(prodCode))
                {
                    deduped75++;
                    continue;
                }
                if (options.Align75Dates && text.Length == 126)
                {
                    string curDi = SintegraParser.SliceCols(text, 3, 10);
                    string curDf = SintegraParser.SliceCols(text, 11, 18);
                    bool needAlign = isMultiMonth
                        ? (string.CompareOrdinal(curDf, r10Di) < 0 || string.CompareOrdinal(curDi, r10Df) > 0)
                        : (curDi != r10Di || curDf != r10Df);

                    if (needAlign)
                    {
                        text = "75" + r10Di + r10Df + text[18..];
                        aligned75Dates++;
                    }
                }
            }

            workingLines.Add((recType, line.Subtype, text));
        }

        if (options.SynthesizeMissing75)
        {
            foreach (var (prodCode, aliqBps) in referencedProds.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (seen75Codes.Add(prodCode))
                {
                    string codeField = PadField(prodCode, 14);
                    string ncmField = "00000000";
                    string descField = PadField($"PRODUTO COD {prodCode}", 53);
                    string unidField = PadField("UN", 6);
                    string ipiField = "00000";
                    long clampedAliq = Math.Clamp(aliqBps, 0L, 9999L);
                    string icmsField = $"{clampedAliq:0000}";
                    string redField = "00000";
                    string stField = "0000000000000";

                    string line75 = $"75{r10Di}{r10Df}{codeField}{ncmField}{descField}{unidField}{ipiField}{icmsField}{redField}{stField}";
                    workingLines.Add(("75", null, PadOrTrimExact(line75)));
                    synthesized75++;
                }
            }
        }

        if (options.RebuildReg90AndSort)
        {
            // Canonical record type order preserving stable intra-record sequence,
            // while sorting contiguous Reg 54 items of the same NF by item number (regra 1008/2099).
            workingLines = workingLines
                .OrderBy(x => RecordSortRank(x.RecType, x.Subtype))
                .ToList();
            SortReg54ItemsInPlace(workingLines);

            // Convenio ICMS 57/95 Record 90 specifications:
            // - Omit 10/11 from Reg 90 (prevents Warning 1014)
            // - Omit any type with count == 0 (prevents Error 2192)
            // - Include all present record types 50..88 ascending + 99 at the end
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var (rt, _, _) in workingLines)
            {
                if (rt is "10" or "11" or "90") continue;
                counts.TryGetValue(rt, out long c);
                counts[rt] = c + 1;
            }

            var pairs = new List<(string Tipo, long Count)>();
            foreach (var (rt, cnt) in counts)
            {
                if (cnt > 0)
                    pairs.Add((rt, cnt));
            }

            int totalBlocks = pairs.Count + 1;
            int numReg90Lines = Math.Max(1, (totalBlocks + 8) / 9);
            long grandTotalLines = workingLines.Count + numReg90Lines;
            pairs.Add(("99", grandTotalLines));

            string numReg90Char = Math.Min(9, numReg90Lines).ToString();
            for (int offset = 0; offset < pairs.Count; offset += 9)
            {
                var chunk = pairs.Skip(offset).Take(9);
                var sbBlocks = new StringBuilder(95);
                foreach (var (tp, cnt) in chunk)
                    sbBlocks.Append($"{PadField(tp, 2)}{cnt:00000000}");
                while (sbBlocks.Length < 95)
                    sbBlocks.Append(' ');

                string reg90Line = $"90{r10Cnpj}{r10Ie}{sbBlocks}{numReg90Char}";
                workingLines.Add(("90", null, PadOrTrimExact(reg90Line)));
            }

            appliedFixes.Add(new AppliedFix("FIX-008", "Ordenação canônica dos registros e recálculo integral do Registro 90.", numReg90Lines));
        }

        if (crlfFixed > 0)
            appliedFixes.Insert(0, new AppliedFix("FIX-001", "Normalização de quebras de linha para CRLF (0x0D 0x0A).", crlfFixed));
        if (lenFixed > 0)
            appliedFixes.Add(new AppliedFix("FIX-002", "Ajuste do comprimento físico das linhas para 126 colunas.", lenFixed));
        if (removedOrphan75 > 0)
            appliedFixes.Add(new AppliedFix("FIX-003", "Remoção de Registros 75 órfãos sem movimentação no período.", removedOrphan75));
        if (synthesized75 > 0)
            appliedFixes.Add(new AppliedFix("FIX-004", "Geração automática de Registros 75 faltantes para produtos movimentados.", synthesized75));
        if (aligned75Dates > 0)
            appliedFixes.Add(new AppliedFix("FIX-005", "Sincronização das datas de validade do Registro 75 com o Registro 10.", aligned75Dates));
        if (deduped75 > 0)
            appliedFixes.Add(new AppliedFix("FIX-006", "Remoção de Registros 75 com código de produto duplicado.", deduped75));
        if (cancelledFixed > 0)
            appliedFixes.Add(new AppliedFix("FIX-007", "Zeramento de valores em Notas Fiscais canceladas (Situação S).", cancelledFixed));

        byte[] fixedBytes = EncodeLinesCrlf(workingLines.Select(x => x.Text));
        string fixedContent = Encoding.Latin1.GetString(fixedBytes);
        var afterDoc = SintegraParser.ParseBytes(fixedBytes);
        var afterRep = SintegraValidator.Validate(afterDoc);

        return new FixReport(appliedFixes, beforeRep.Summary, afterRep.Summary, fixedContent, fixedBytes);
    }

    public static IReadOnlyList<MonthlySplitResult> SplitAnnualByMonth(byte[] rawBytes)
    {
        var doc = SintegraParser.ParseBytes(rawBytes);
        var report = SintegraValidator.Validate(doc);
        if (doc.Reg10 is null || doc.Reg11 is null)
            return Array.Empty<MonthlySplitResult>();

        var line10 = doc.Lines.FirstOrDefault(l => l.RecordType == "10")?.RawText ?? "";
        var line11 = doc.Lines.FirstOrDefault(l => l.RecordType == "11")?.RawText ?? "";
        if (line10.Length < 126 || line11.Length < 126)
            return Array.Empty<MonthlySplitResult>();

        string r10Cnpj = PadField(doc.Reg10.Cnpj, 14);
        string r10Ie = PadField(doc.Reg10.Ie, 14);
        var results = new List<MonthlySplitResult>();

        // Index 75 templates by product code
        var r75ByCode = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var l in doc.Lines.Where(x => x.RecordType == "75" && x.RawText.Length >= 126))
        {
            string c = SintegraParser.SliceCols(l.RawText, 19, 32).Trim();
            r75ByCode.TryAdd(c, l.RawText[..126]);
        }

        // Index Reg50 keys -> month
        var r50MonthByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r50 in doc.Reg50)
        {
            if (r50.DataEmissao.Length >= 6)
            {
                string key = $"{r50.Cnpj}|{r50.Modelo}|{r50.Serie}|{r50.Numero}";
                r50MonthByKey.TryAdd(key, r50.DataEmissao[..6]);
            }
        }

        foreach (var mb in report.MonthlyBalance)
        {
            string ym = mb.YearMonth;
            if (ym.Length != 6 || !ym.All(char.IsAsciiDigit)) continue;

            int year = int.Parse(ym[..4]);
            int month = int.Parse(ym[4..6]);
            string di = $"{year:0000}{month:00}01";
            string maxDay = $"{year:0000}{month:00}{DateTime.DaysInMonth(year, month):00}";
            string df = string.CompareOrdinal(doc.Reg10.DataFinal, maxDay) < 0 && doc.Reg10.DataFinal[..6] == ym
                ? doc.Reg10.DataFinal
                : maxDay;

            string m10 = line10[..107] + di + df + line10[123..126];
            string m11 = PadOrTrimExact(line11);

            var lines = new List<(string RecType, string? Subtype, string Text)>
            {
                ("10", null, m10),
                ("11", null, m11)
            };

            var usedProducts = new SortedSet<string>(StringComparer.Ordinal);
            bool hasSaidaInMonth = false;

            foreach (var r50 in doc.Reg50.Where(x => x.DataEmissao.StartsWith(ym, StringComparison.Ordinal)))
            {
                lines.Add(("50", null, PadOrTrimExact(doc.Lines[r50.Line - 1].RawText)));
                if (!TaxIdValidator.IsEntradaCfop(r50.Cfop)) hasSaidaInMonth = true;
            }

            foreach (var r54 in doc.Reg54)
            {
                string key = $"{r54.Cnpj}|{r54.Modelo}|{r54.Serie}|{r54.Numero}";
                if (r50MonthByKey.TryGetValue(key, out string? nfYm) && nfYm == ym)
                {
                    lines.Add(("54", null, PadOrTrimExact(doc.Lines[r54.Line - 1].RawText)));
                    if (r54.NumItem is not ("991" or "992" or "999") && !string.IsNullOrEmpty(r54.CodProduto))
                        usedProducts.Add(r54.CodProduto);
                }
            }

            foreach (var r61 in doc.Reg61.Where(x => x.DataEmissao.StartsWith(ym, StringComparison.Ordinal)))
            {
                lines.Add(("61", null, PadOrTrimExact(doc.Lines[r61.Line - 1].RawText)));
                hasSaidaInMonth = true;
            }

            string mmYyyy = $"{month:00}{year:0000}";
            foreach (var r61r in doc.Reg61R.Where(x => x.MesAno == mmYyyy))
            {
                lines.Add(("61", "R", PadOrTrimExact(doc.Lines[r61r.Line - 1].RawText)));
                if (!string.IsNullOrEmpty(r61r.CodProduto))
                    usedProducts.Add(r61r.CodProduto);
            }

            foreach (var r70 in doc.Reg70.Where(x => x.DataEmissao.StartsWith(ym, StringComparison.Ordinal)))
            {
                lines.Add(("70", null, PadOrTrimExact(doc.Lines[r70.Line - 1].RawText)));
            }

            foreach (string code in usedProducts)
            {
                if (r75ByCode.TryGetValue(code, out string? tpl75))
                {
                    lines.Add(("75", null, "75" + di + df + tpl75[18..126]));
                }
                else
                {
                    string line75 = $"75{di}{df}{PadField(code, 14)}00000000{PadField($"PRODUTO COD {code}", 53)}{PadField("UN", 6)}000000000000000000000000000";
                    lines.Add(("75", null, PadOrTrimExact(line75)));
                }
            }

            if (!hasSaidaInMonth)
            {
                string line88 = $"88SMS{r10Cnpj}SEM MOVIMENTO DE SAIDA".PadRight(126, ' ');
                lines.Add(("88", "SMS", line88));
            }

            // Build valid Reg 90
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var (rt, _, _) in lines)
            {
                if (rt is "10" or "11") continue;
                counts.TryGetValue(rt, out long c);
                counts[rt] = c + 1;
            }
            var pairs = counts.Where(kv => kv.Value > 0).Select(kv => (kv.Key, kv.Value)).ToList();
            int num90 = Math.Max(1, (pairs.Count + 1 + 8) / 9);
            pairs.Add(("99", lines.Count + num90));
            string num90Char = Math.Min(9, num90).ToString();
            for (int off = 0; off < pairs.Count; off += 9)
            {
                var sbBlocks = new StringBuilder(95);
                foreach (var (tp, cnt) in pairs.Skip(off).Take(9))
                    sbBlocks.Append($"{PadField(tp, 2)}{cnt:00000000}");
                while (sbBlocks.Length < 95) sbBlocks.Append(' ');
                lines.Add(("90", null, PadOrTrimExact($"90{r10Cnpj}{r10Ie}{sbBlocks}{num90Char}")));
            }

            byte[] mBytes = EncodeLinesCrlf(lines.Select(x => x.Text));
            var mDoc = SintegraParser.ParseBytes(mBytes);
            var mRep = SintegraValidator.Validate(mDoc);
            results.Add(new MonthlySplitResult(
                ym,
                mb.MonthLabel,
                $"SINTEGRA_{ym}.TXT",
                lines.Count,
                mBytes,
                mRep.Summary));
        }

        return results;
    }

    private static void SortReg54ItemsInPlace(List<(string RecType, string? Subtype, string Text)> lines)
    {
        int i = 0;
        while (i < lines.Count)
        {
            if (lines[i].RecType != "54" || lines[i].Text.Length < 37)
            {
                i++;
                continue;
            }
            string nfKey = lines[i].Text[2..27];
            int j = i + 1;
            while (j < lines.Count && lines[j].RecType == "54" && lines[j].Text.Length >= 37 && lines[j].Text[2..27] == nfKey)
                j++;

            if (j - i > 1)
            {
                var slice = lines.GetRange(i, j - i);
                slice.Sort((a, b) =>
                {
                    int ia = int.TryParse(a.Text[34..37].Trim(), out int va) ? va : 999;
                    int ib = int.TryParse(b.Text[34..37].Trim(), out int vb) ? vb : 999;
                    return ia.CompareTo(ib);
                });
                for (int k = 0; k < slice.Count; k++)
                    lines[i + k] = slice[k];
            }
            i = j;
        }
    }

    private static byte[] EncodeLinesCrlf(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        foreach (string text in lines)
        {
            sb.Append(text);
            sb.Append("\r\n");
        }
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    public static string BuildDemoBrokenSintegra()
    {
        // Every single line uses exact 126-char columns (valid CNPJs/IEs from Temp.txt),
        // with realistic fixable defects: cancelled NF with values, missing Reg 75, orphan Reg 75, wrong Reg 90 + LF-only ending.
        string[] lines =
        [
            "10635643570001120053520140004 MG DISTRIBUIDORA SAO ROMAO LTDA    SAO ROMAO                     MG00000000002026080120260831331",
            "11R ENOCH DE ASSIS CARDOSO, 323     00000R ENOCH DE ASSIS CARDOCENTRO         39290000MARQUES                     038998154966",
            "50712432570004654338483730387 20260805MG551  4472961403T000000007090000000000000000000000000000000000000000000000000000000000N",
            $"5063564357000112{PadField("0053520140004", 14)}20260810MG55{PadField("1", 3)}0001055102P{125000:0000000000000}{125000:0000000000000}{22500:0000000000000}{0:0000000000000}{0:0000000000000}1800N",
            $"5063564357000112{PadField("0053520140004", 14)}20260811MG55{PadField("1", 3)}0001065102P{50000:0000000000000}{50000:0000000000000}{9000:0000000000000}{0:0000000000000}{0:0000000000000}1800S",
            "5471243257000465551  4472961403060001000369        000002400000000000709000000000000000000000000000000000000000000000000000000",
            "5463564357000112551  0001055102000001000888        000001000000000001250000000000000000000012500000000000000000000000000001800",
            "752026080120260831000369        22030000ORIGINAL LITRIM  300ML                               DZ    000000000000000000000000400",
            "752026070120260731999999        22021000PRODUTO ORFAO SEM MOVIMENTO NO PERIODO               UN    000000180000000000000000000",
            "90635643570001120053520140004 50000000015100000000540000000175000000019900000006                                             1"
        ];
        // Line 2 intentionally uses LF only to demonstrate FIX-001 CRLF normalization cleanly
        return lines[0] + "\r\n" + lines[1] + "\n" + string.Join("\r\n", lines[2..]) + "\r\n";
    }
}
