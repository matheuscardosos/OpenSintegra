using System.Security.Cryptography;
using System.Text;

namespace OpenSintegra.Core;

public static class SintegraParser
{
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
            table[i] = c;
        }
        return table;
    }

    public static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in bytes)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }

    public static string SliceCols(string line, int start1Based, int end1Based)
    {
        if (start1Based <= 0 || start1Based > end1Based || start1Based > line.Length)
            return string.Empty;
        int s = start1Based - 1;
        int e = Math.Min(end1Based, line.Length);
        return line[s..e];
    }

    public static long ParseLongField(string line, int start1Based, int end1Based)
    {
        var s = SliceCols(line, start1Based, end1Based).Trim();
        return long.TryParse(s, out long val) ? val : 0L;
    }

    public static string DecodeLatin1OrUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return utf8.GetString(bytes);
        }
        catch
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    public static SintegraDocument ParseBytes(
        byte[] bytes,
        IProgress<(int Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        uint crc = ComputeCrc32(bytes);
        string crcHex = $"{crc:X8}";
        byte[] shaBytes = SHA256.HashData(bytes);
        string shaHex = Convert.ToHexString(shaBytes).ToLowerInvariant();

        var doc = new SintegraDocument
        {
            ByteSize = bytes.Length,
            Crc32Hex = crcHex,
            Sha256Hex = shaHex
        };

        int i = 0;
        int lineNumber = 0;
        int totalLen = Math.Max(1, bytes.Length);
        int lastPct = -1;
        cancellationToken.ThrowIfCancellationRequested();
        while (i < bytes.Length)
        {
            int start = i;
            while (i < bytes.Length && bytes[i] != (byte)'\r' && bytes[i] != (byte)'\n')
                i++;

            int contentEnd = i;
            bool hasCr = false;
            bool hasLf = false;
            if (i < bytes.Length && bytes[i] == (byte)'\r')
            {
                hasCr = true;
                i++;
            }
            if (i < bytes.Length && bytes[i] == (byte)'\n')
            {
                hasLf = true;
                i++;
            }

            var lineBytes = bytes.AsSpan(start, contentEnd - start);
            if (lineBytes.Length == 0 && i == bytes.Length)
                break;
            if (lineBytes.Length == 1 && lineBytes[0] == 0x1A && i == bytes.Length)
                break;

            lineNumber++;
            if (lineNumber % 250 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int pct = Math.Clamp((int)(i * 100L / totalLen), 1, 99);
                if (pct != lastPct)
                {
                    lastPct = pct;
                    progress?.Report((pct, $"Lendo registro linha {lineNumber:N0}..."));
                }
            }
            string rawText = DecodeLatin1OrUtf8(lineBytes);
            int charLen = rawText.Length;
            string recType = SliceCols(rawText, 1, 2);

            string? subtype = recType switch
            {
                "60" => SliceCols(rawText, 3, 3),
                "61" => SliceCols(rawText, 3, 3) == "R" ? "R" : null,
                "88" => SliceCols(rawText, 3, 6).Trim(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(recType))
            {
                doc.PhysicalCounts.TryGetValue(recType, out long curCount);
                doc.PhysicalCounts[recType] = curCount + 1;
            }

            switch (recType)
            {
                case "10":
                    doc.Reg10 = new Reg10(
                        lineNumber,
                        SliceCols(rawText, 3, 16),
                        SliceCols(rawText, 17, 30).Trim(),
                        SliceCols(rawText, 31, 65).Trim(),
                        SliceCols(rawText, 66, 95).Trim(),
                        SliceCols(rawText, 96, 97),
                        SliceCols(rawText, 98, 107).Trim(),
                        SliceCols(rawText, 108, 115),
                        SliceCols(rawText, 116, 123),
                        SliceCols(rawText, 124, 124),
                        SliceCols(rawText, 125, 125),
                        SliceCols(rawText, 126, 126)
                    );
                    break;

                case "11":
                    doc.Reg11 = new Reg11(
                        lineNumber,
                        SliceCols(rawText, 3, 36).Trim(),
                        SliceCols(rawText, 37, 41).Trim(),
                        SliceCols(rawText, 42, 63).Trim(),
                        SliceCols(rawText, 64, 78).Trim(),
                        SliceCols(rawText, 79, 86),
                        SliceCols(rawText, 87, 114).Trim(),
                        SliceCols(rawText, 115, 126).Trim()
                    );
                    break;

                case "50":
                    doc.Reg50.Add(new Reg50(
                        lineNumber,
                        SliceCols(rawText, 3, 16),
                        SliceCols(rawText, 17, 30).Trim(),
                        SliceCols(rawText, 31, 38),
                        SliceCols(rawText, 39, 40),
                        SliceCols(rawText, 41, 42),
                        SliceCols(rawText, 43, 45).Trim(),
                        SliceCols(rawText, 46, 51),
                        SliceCols(rawText, 52, 55),
                        SliceCols(rawText, 56, 56),
                        ParseLongField(rawText, 57, 69),
                        ParseLongField(rawText, 70, 82),
                        ParseLongField(rawText, 83, 95),
                        ParseLongField(rawText, 96, 108),
                        ParseLongField(rawText, 109, 121),
                        ParseLongField(rawText, 122, 125),
                        SliceCols(rawText, 126, 126)
                    ));
                    break;

                case "51":
                    doc.Reg51.Add(new Reg51(
                        lineNumber,
                        SliceCols(rawText, 3, 16),
                        SliceCols(rawText, 17, 30).Trim(),
                        SliceCols(rawText, 31, 38),
                        SliceCols(rawText, 39, 40),
                        SliceCols(rawText, 41, 43).Trim(),
                        SliceCols(rawText, 44, 49),
                        SliceCols(rawText, 50, 53),
                        ParseLongField(rawText, 54, 66),
                        ParseLongField(rawText, 67, 79),
                        ParseLongField(rawText, 80, 92),
                        ParseLongField(rawText, 93, 105),
                        SliceCols(rawText, 126, 126)
                    ));
                    break;

                case "53":
                    doc.Reg53.Add(new Reg53(
                        lineNumber,
                        SliceCols(rawText, 3, 16),
                        SliceCols(rawText, 17, 30).Trim(),
                        SliceCols(rawText, 31, 38),
                        SliceCols(rawText, 39, 40),
                        SliceCols(rawText, 41, 42),
                        SliceCols(rawText, 43, 45).Trim(),
                        SliceCols(rawText, 46, 51),
                        SliceCols(rawText, 52, 55),
                        SliceCols(rawText, 56, 56),
                        ParseLongField(rawText, 57, 69),
                        ParseLongField(rawText, 70, 82),
                        ParseLongField(rawText, 83, 95),
                        SliceCols(rawText, 96, 96),
                        SliceCols(rawText, 97, 97)
                    ));
                    break;

                case "54":
                    doc.Reg54.Add(new Reg54(
                        lineNumber,
                        SliceCols(rawText, 3, 16),
                        SliceCols(rawText, 17, 18),
                        SliceCols(rawText, 19, 21).Trim(),
                        SliceCols(rawText, 22, 27),
                        SliceCols(rawText, 28, 31),
                        SliceCols(rawText, 32, 34),
                        SliceCols(rawText, 35, 37),
                        SliceCols(rawText, 38, 51).Trim(),
                        ParseLongField(rawText, 52, 62),
                        ParseLongField(rawText, 63, 74),
                        ParseLongField(rawText, 75, 86),
                        ParseLongField(rawText, 87, 98),
                        ParseLongField(rawText, 99, 110),
                        ParseLongField(rawText, 111, 122),
                        ParseLongField(rawText, 123, 126)
                    ));
                    break;

                case "60":
                    switch (subtype)
                    {
                        case "M":
                            doc.Reg60M.Add(new Reg60M(
                                lineNumber,
                                SliceCols(rawText, 4, 11),
                                SliceCols(rawText, 12, 31).Trim(),
                                SliceCols(rawText, 32, 34),
                                SliceCols(rawText, 35, 36),
                                SliceCols(rawText, 37, 42),
                                SliceCols(rawText, 43, 48),
                                SliceCols(rawText, 49, 54),
                                SliceCols(rawText, 55, 57),
                                ParseLongField(rawText, 58, 73),
                                ParseLongField(rawText, 74, 89)
                            ));
                            break;
                        case "A":
                            doc.Reg60A.Add(new Reg60A(
                                lineNumber,
                                SliceCols(rawText, 4, 11),
                                SliceCols(rawText, 12, 31).Trim(),
                                SliceCols(rawText, 32, 35).Trim(),
                                ParseLongField(rawText, 36, 47)
                            ));
                            break;
                        case "D":
                            doc.Reg60D.Add(new Reg60D(
                                lineNumber,
                                SliceCols(rawText, 4, 11),
                                SliceCols(rawText, 12, 31).Trim(),
                                SliceCols(rawText, 32, 45).Trim(),
                                ParseLongField(rawText, 46, 58),
                                ParseLongField(rawText, 59, 74),
                                ParseLongField(rawText, 75, 90),
                                SliceCols(rawText, 91, 94).Trim(),
                                ParseLongField(rawText, 95, 107)
                            ));
                            break;
                        case "I":
                            doc.Reg60I.Add(new Reg60I(
                                lineNumber,
                                SliceCols(rawText, 4, 11),
                                SliceCols(rawText, 12, 31).Trim(),
                                SliceCols(rawText, 32, 33),
                                SliceCols(rawText, 34, 39),
                                SliceCols(rawText, 40, 42),
                                SliceCols(rawText, 43, 56).Trim(),
                                ParseLongField(rawText, 57, 69),
                                ParseLongField(rawText, 70, 82),
                                ParseLongField(rawText, 83, 94),
                                SliceCols(rawText, 95, 98).Trim(),
                                ParseLongField(rawText, 99, 110)
                            ));
                            break;
                        case "R":
                            doc.Reg60R.Add(new Reg60R(
                                lineNumber,
                                SliceCols(rawText, 4, 9),
                                SliceCols(rawText, 10, 23).Trim(),
                                ParseLongField(rawText, 24, 36),
                                ParseLongField(rawText, 37, 52),
                                ParseLongField(rawText, 53, 68),
                                ParseLongField(rawText, 69, 72)
                            ));
                            break;
                    }
                    break;

                case "61":
                    if (subtype == "R")
                    {
                        doc.Reg61R.Add(new Reg61R(
                            lineNumber,
                            SliceCols(rawText, 4, 9),
                            SliceCols(rawText, 10, 23).Trim(),
                            ParseLongField(rawText, 24, 36),
                            ParseLongField(rawText, 37, 52),
                            ParseLongField(rawText, 53, 68),
                            ParseLongField(rawText, 69, 72)
                        ));
                    }
                    else
                    {
                        doc.Reg61.Add(new Reg61(
                            lineNumber,
                            SliceCols(rawText, 31, 38),
                            SliceCols(rawText, 39, 40),
                            SliceCols(rawText, 41, 43).Trim(),
                            SliceCols(rawText, 44, 45).Trim(),
                            SliceCols(rawText, 46, 51),
                            SliceCols(rawText, 52, 57),
                            ParseLongField(rawText, 58, 70),
                            ParseLongField(rawText, 71, 83),
                            ParseLongField(rawText, 84, 95),
                            ParseLongField(rawText, 96, 108),
                            ParseLongField(rawText, 109, 121),
                            ParseLongField(rawText, 122, 125)
                        ));
                    }
                    break;

                case "57":
                {
                    string c = SliceCols(rawText, 38, 51).Trim();
                    if (!string.IsNullOrEmpty(c)) doc.OtherProductCodes.Add(c);
                    break;
                }

                case "77":
                {
                    string c = SliceCols(rawText, 32, 45).Trim();
                    if (!string.IsNullOrEmpty(c)) doc.OtherProductCodes.Add(c);
                    break;
                }

                case "86":
                {
                    string c = SliceCols(rawText, 55, 68).Trim();
                    if (!string.IsNullOrEmpty(c)) doc.OtherProductCodes.Add(c);
                    break;
                }

                case "70":
                    doc.Reg70.Add(new Reg70(
                        lineNumber,
                        SliceCols(rawText, 3, 16),
                        SliceCols(rawText, 17, 30).Trim(),
                        SliceCols(rawText, 31, 38),
                        SliceCols(rawText, 39, 40),
                        SliceCols(rawText, 41, 42),
                        SliceCols(rawText, 43, 43),
                        SliceCols(rawText, 44, 45).Trim(),
                        SliceCols(rawText, 46, 51),
                        SliceCols(rawText, 52, 55),
                        ParseLongField(rawText, 56, 68),
                        ParseLongField(rawText, 69, 82),
                        ParseLongField(rawText, 83, 96),
                        ParseLongField(rawText, 97, 110),
                        ParseLongField(rawText, 111, 124),
                        SliceCols(rawText, 125, 125),
                        SliceCols(rawText, 126, 126)
                    ));
                    break;

                case "74":
                    doc.Reg74.Add(new Reg74(
                        lineNumber,
                        SliceCols(rawText, 3, 10),
                        SliceCols(rawText, 11, 24).Trim(),
                        ParseLongField(rawText, 25, 37),
                        ParseLongField(rawText, 38, 50),
                        SliceCols(rawText, 51, 51),
                        SliceCols(rawText, 52, 65),
                        SliceCols(rawText, 66, 79).Trim(),
                        SliceCols(rawText, 80, 81)
                    ));
                    break;

                case "75":
                    doc.Reg75.Add(new Reg75(
                        lineNumber,
                        SliceCols(rawText, 3, 10),
                        SliceCols(rawText, 11, 18),
                        SliceCols(rawText, 19, 32).Trim(),
                        SliceCols(rawText, 33, 40).Trim(),
                        SliceCols(rawText, 41, 93).Trim(),
                        SliceCols(rawText, 94, 99).Trim(),
                        ParseLongField(rawText, 100, 104),
                        ParseLongField(rawText, 105, 108),
                        ParseLongField(rawText, 109, 113),
                        ParseLongField(rawText, 114, 126)
                    ));
                    break;

                case "90":
                    var counters = new List<Reg90Counter>();
                    int pos = 31;
                    while (pos + 9 <= 125 && pos + 9 <= rawText.Length)
                    {
                        string tp = SliceCols(rawText, pos, pos + 1);
                        string cntStr = SliceCols(rawText, pos + 2, pos + 9).Trim();
                        if (!string.IsNullOrWhiteSpace(tp) && cntStr.Length > 0 && cntStr.All(char.IsAsciiDigit))
                        {
                            long val = long.Parse(cntStr);
                            counters.Add(new Reg90Counter(tp, val));
                        }
                        pos += 10;
                    }
                    doc.Reg90.Add(new Reg90(
                        lineNumber,
                        SliceCols(rawText, 3, 16),
                        SliceCols(rawText, 17, 30).Trim(),
                        counters,
                        SliceCols(rawText, 126, 126)
                    ));
                    break;
            }

            doc.Lines.Add(new RawLine(
                lineNumber,
                recType,
                subtype,
                rawText,
                charLen,
                hasCr,
                hasLf
            ));
        }

        return doc;
    }
}
