using System.Text;

namespace OpenSintegra.Tests;

internal static class SintegraFixtureBuilder
{
    public static string Reg10(string cnpj, string ie, string nome, string di, string df) =>
        $"10{cnpj,-14}{ie,-14}{nome,-35}{"CAMPO GRANDE",-30}MS0000000000{di}{df}331";

    public static string Reg11() =>
        $"11{"RUA BARAO DO RIO BRANCO",-34}00100{"SALA 2",-22}{"CENTRO",-15}79002000{"CONTADOR RESPONSAVEL",-28}067330011220";

    public static string Reg50(string cnpj, string ymd, string numero6, string cfop, long valCents, string emitente = "T")
    {
        long icms = valCents * 12 / 100;
        return $"50{cnpj,-14}{"ISENTO",-14}{ymd}MS55001{numero6}{cfop}{emitente}{valCents:0000000000000}{valCents:0000000000000}{icms:0000000000000}{0:0000000000000}{0:0000000000000}1200N";
    }

    public static string Reg54(string cnpj, string numero6, string cfop, string itemNum3, string codProd, long valCents) =>
        $"54{cnpj,-14}55001{numero6}{cfop}000{itemNum3}{codProd,-14}{1000:00000000000}{valCents:000000000000}{0:000000000000}{valCents:000000000000}{0:000000000000}{0:000000000000}1200";

    public static string Reg61(string ymd, long valCents) =>
        $"61{new string(' ', 28)}{ymd}65001  000001000010{valCents:0000000000000}{valCents:0000000000000}{0:000000000000}{0:0000000000000}{0:0000000000000}1200 ";

    public static string Reg61R(string mmYyyy, string codProd, long valCents) =>
        $"61R{mmYyyy}{codProd,-14}{1000:0000000000000}{valCents:0000000000000000}{valCents:0000000000000000}1200{new string(' ', 54)}";

    public static string Reg75(string di, string df, string codProd, string desc) =>
        $"75{di}{df}{codProd,-14}02013000{desc,-53}{"UN",-6}000001200000000000000000000";

    public static string Reg90(string cnpj, string ie, params (string Kind, long Count)[] entries)
    {
        var sb = new StringBuilder(95);
        foreach (var (k, c) in entries)
            sb.Append($"{k,-2}{c:00000000}");
        while (sb.Length < 95) sb.Append(' ');
        return $"90{cnpj,-14}{ie,-14}{sb}1";
    }
}
