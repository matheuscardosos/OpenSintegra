namespace OpenSintegra.Core;

public static class TaxIdValidator
{
    public static readonly HashSet<string> ValidUfs = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT",
        "PA", "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO", "EX"
    };

    public static readonly HashSet<string> ValidModels50 = new(StringComparer.OrdinalIgnoreCase)
    {
        "01", "02", "04", "06", "21", "22", "55", "65"
    };

    public static readonly HashSet<string> ValidModels70 = new(StringComparer.OrdinalIgnoreCase)
    {
        "07", "08", "09", "10", "11", "26", "27", "57", "67"
    };

    public static bool IsValidUf(string uf) => ValidUfs.Contains(uf.Trim());

    public static bool ValidateCnpj(string input)
    {
        var s = input.Trim();
        if (s.Length != 14 || !s.All(char.IsAsciiDigit))
            return false;
        if (s.All(c => c == s[0]))
            return false;

        Span<int> digits = stackalloc int[14];
        for (int i = 0; i < 14; i++)
            digits[i] = s[i] - '0';

        ReadOnlySpan<int> w1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int sum1 = 0;
        for (int i = 0; i < 12; i++)
            sum1 += digits[i] * w1[i];
        int rem1 = sum1 % 11;
        int dv1 = rem1 < 2 ? 0 : 11 - rem1;
        if (digits[12] != dv1)
            return false;

        ReadOnlySpan<int> w2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int sum2 = 0;
        for (int i = 0; i < 13; i++)
            sum2 += digits[i] * w2[i];
        int rem2 = sum2 % 11;
        int dv2 = rem2 < 2 ? 0 : 11 - rem2;
        return digits[13] == dv2;
    }

    public static bool ValidateCpf(string input)
    {
        var s = input.Trim();
        if (s.Length == 14 && s.StartsWith("000", StringComparison.Ordinal))
            s = s[3..];
        if (s.Length != 11 || !s.All(char.IsAsciiDigit))
            return false;
        if (s.All(c => c == s[0]))
            return false;

        Span<int> digits = stackalloc int[11];
        for (int i = 0; i < 11; i++)
            digits[i] = s[i] - '0';

        int sum1 = 0;
        for (int i = 0; i < 9; i++)
            sum1 += digits[i] * (10 - i);
        int rem1 = (sum1 * 10) % 11;
        int dv1 = rem1 == 10 ? 0 : rem1;
        if (digits[9] != dv1)
            return false;

        int sum2 = 0;
        for (int i = 0; i < 10; i++)
            sum2 += digits[i] * (11 - i);
        int rem2 = (sum2 * 10) % 11;
        int dv2 = rem2 == 10 ? 0 : rem2;
        return digits[10] == dv2;
    }

    public static bool ValidateCnpjOrCpf(string input)
    {
        var s = input.Trim();
        if (s == "00000000000000")
            return true;
        return ValidateCnpj(s) || ValidateCpf(s);
    }

    public static bool ValidateIe(string uf, string ieRaw)
    {
        var ie = ieRaw.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(ie))
            return false;
        if (ie == "ISENTO" || ie.All(c => c == '0'))
            return true;

        var digits = ie.Where(char.IsAsciiDigit).Select(c => c - '0').ToArray();
        if (digits.Length == 0)
            return false;

        return uf.Trim().ToUpperInvariant() switch
        {
            "MG" => ValidateIeMg(digits),
            "SP" => ie.StartsWith('P') ? digits.Length == 12 : ValidateIeSp(digits),
            "RS" => ValidateIeRs(digits),
            "PR" => ValidateIePr(digits),
            "SC" => ValidateIeSc(digits),
            "RJ" => ValidateIeRj(digits),
            "ES" => ValidateIeEs(digits),
            "BA" => digits.Length is 8 or 9,
            "EX" => true,
            _ => digits.Length is >= 7 and <= 14
        };
    }

    private static bool ValidateIeMg(int[] d)
    {
        if (d.Length != 13)
            return false;
        Span<int> a = stackalloc int[12];
        a[0] = d[0];
        a[1] = d[1];
        a[2] = d[2];
        a[3] = 0;
        for (int i = 3; i < 11; i++)
            a[i + 1] = d[i];

        int sum1 = 0;
        for (int i = 0; i < 12; i++)
        {
            int weight = i % 2 == 0 ? 1 : 2;
            int prod = a[i] * weight;
            sum1 += (prod / 10) + (prod % 10);
        }
        int nextTen = ((sum1 / 10) + 1) * 10;
        int dv1 = nextTen - sum1;
        if (dv1 == 10)
            dv1 = 0;
        if (d[11] != dv1)
            return false;

        ReadOnlySpan<int> w2 = [3, 2, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2];
        int sum2 = 0;
        for (int i = 0; i < 12; i++)
            sum2 += d[i] * w2[i];
        int rem2 = sum2 % 11;
        int dv2 = rem2 < 2 ? 0 : 11 - rem2;
        return d[12] == dv2;
    }

    private static bool ValidateIeSp(int[] d)
    {
        if (d.Length != 12)
            return false;
        ReadOnlySpan<int> w1 = [1, 3, 4, 5, 6, 7, 8, 10];
        int sum1 = 0;
        for (int i = 0; i < 8; i++)
            sum1 += d[i] * w1[i];
        int dv1 = (sum1 % 11) % 10;
        if (d[8] != dv1)
            return false;

        ReadOnlySpan<int> w2 = [3, 2, 10, 9, 8, 7, 6, 5, 4, 3, 2];
        int sum2 = 0;
        for (int i = 0; i < 11; i++)
            sum2 += d[i] * w2[i];
        int dv2 = (sum2 % 11) % 10;
        return d[11] == dv2;
    }

    private static bool ValidateIeRs(int[] d)
    {
        if (d.Length != 10)
            return false;
        ReadOnlySpan<int> w = [2, 9, 8, 7, 6, 5, 4, 3, 2];
        int sum = 0;
        for (int i = 0; i < 9; i++)
            sum += d[i] * w[i];
        int rem = 11 - (sum % 11);
        int dv = rem >= 10 ? 0 : rem;
        return d[9] == dv;
    }

    private static bool ValidateIePr(int[] d)
    {
        if (d.Length != 10)
            return false;
        ReadOnlySpan<int> w1 = [3, 2, 7, 6, 5, 4, 3, 2];
        int sum1 = 0;
        for (int i = 0; i < 8; i++)
            sum1 += d[i] * w1[i];
        int rem1 = 11 - (sum1 % 11);
        int dv1 = rem1 >= 10 ? 0 : rem1;
        if (d[8] != dv1)
            return false;

        ReadOnlySpan<int> w2 = [4, 3, 2, 7, 6, 5, 4, 3, 2];
        int sum2 = 0;
        for (int i = 0; i < 9; i++)
            sum2 += d[i] * w2[i];
        int rem2 = 11 - (sum2 % 11);
        int dv2 = rem2 >= 10 ? 0 : rem2;
        return d[9] == dv2;
    }

    private static bool ValidateIeSc(int[] d)
    {
        if (d.Length != 9)
            return false;
        ReadOnlySpan<int> w = [9, 8, 7, 6, 5, 4, 3, 2];
        int sum = 0;
        for (int i = 0; i < 8; i++)
            sum += d[i] * w[i];
        int rem = sum % 11;
        int dv = rem < 2 ? 0 : 11 - rem;
        return d[8] == dv;
    }

    private static bool ValidateIeRj(int[] d)
    {
        if (d.Length != 8)
            return false;
        ReadOnlySpan<int> w = [2, 7, 6, 5, 4, 3, 2];
        int sum = 0;
        for (int i = 0; i < 7; i++)
            sum += d[i] * w[i];
        int rem = sum % 11;
        int dv = rem < 2 ? 0 : 11 - rem;
        return d[7] == dv;
    }

    private static bool ValidateIeEs(int[] d)
    {
        if (d.Length != 9)
            return false;
        ReadOnlySpan<int> w = [9, 8, 7, 6, 5, 4, 3, 2];
        int sum = 0;
        for (int i = 0; i < 8; i++)
            sum += d[i] * w[i];
        int rem = sum % 11;
        int dv = rem < 2 ? 0 : 11 - rem;
        return d[8] == dv;
    }

    public static bool ValidateDateYyyyMmDd(string dateStr)
    {
        var s = dateStr.Trim();
        if (s.Length != 8 || !s.All(char.IsAsciiDigit))
            return false;
        int year = int.Parse(s[0..4]);
        int month = int.Parse(s[4..6]);
        int day = int.Parse(s[6..8]);
        if (year is < 1990 or > 2100 || month is < 1 or > 12 || day < 1)
            return false;
        return day <= DateTime.DaysInMonth(year, month);
    }

    public static bool ValidateCfop(string cfop)
    {
        var s = cfop.Trim();
        if (s.Length != 4 || !s.All(char.IsAsciiDigit))
            return false;
        return s[0] is '1' or '2' or '3' or '5' or '6' or '7';
    }

    public static string CfopNature(string cfop)
    {
        var s = cfop.Trim();
        if (s.Length == 0)
            return "Outros";
        return s[0] switch
        {
            '1' => "Entrada Estadual",
            '2' => "Entrada Interestadual",
            '3' => "Entrada Exterior / Importação",
            '5' => "Saída Estadual",
            '6' => "Saída Interestadual",
            '7' => "Saída Exterior / Exportação",
            _ => "Outros"
        };
    }

    public static bool IsEntradaCfop(string cfop)
    {
        var s = cfop.Trim();
        return s.Length > 0 && s[0] is '1' or '2' or '3';
    }

    public static bool IsCompraComercialCfop(string cfop)
    {
        var s = cfop.Trim();
        if (s.Length != 4)
            return false;
        if (s is "1410" or "1411" or "1414" or "1415" or "2410" or "2411" or "2414" or "2415" or
                 "1660" or "1661" or "1662" or "2660" or "2661" or "2662")
            return false;
        return s[0] is '1' or '2' or '3' && s[1] is '1' or '4' or '6';
    }

    public static bool ValidateMmYyyy(string mmyyyy)
    {
        var s = mmyyyy.Trim();
        if (s.Length != 6 || !s.All(char.IsAsciiDigit))
            return false;
        int month = int.Parse(s[0..2]);
        int year = int.Parse(s[2..6]);
        return month is >= 1 and <= 12 && year is >= 1990 and <= 2100;
    }

    public static string FormatMonthLabel(string yyyyMm)
    {
        var s = yyyyMm.Trim();
        if (s.Length == 6 && s.All(char.IsAsciiDigit))
            return $"{s[4..6]}/{s[0..4]}";
        return s;
    }

    public static string FormatCnpj(string cnpj)
    {
        var s = cnpj.Trim();
        if (s.Length == 14 && s.All(char.IsAsciiDigit))
            return $"{s[0..2]}.{s[2..5]}.{s[5..8]}/{s[8..12]}-{s[12..14]}";
        return s;
    }

    public static string FormatDateBr(string yyyymmdd)
    {
        var s = yyyymmdd.Trim();
        if (s.Length == 8 && s.All(char.IsAsciiDigit))
            return $"{s[6..8]}/{s[4..6]}/{s[0..4]}";
        return s;
    }

    public static string FormatCentsBrl(long cents)
    {
        decimal val = cents / 100.0m;
        return val.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    }

    public static string FormatMilliBr(long milli)
    {
        decimal val = milli / 1000.0m;
        return val.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    }
}
