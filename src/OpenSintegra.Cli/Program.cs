using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSintegra.Core;
using OpenSintegra.Data;
using OpenSintegra.Reports;

namespace OpenSintegra.Cli;

internal static class Program
{
    private const string AppVersion = "1.0.0";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        if (args[0] is "-v" or "--version" or "version")
        {
            Console.WriteLine($"OpenSintegra CLI v{AppVersion}");
            return 0;
        }

        string cmd = args[0].ToLowerInvariant();
        string[] rest = args.Skip(1).ToArray();

        try
        {
            return cmd switch
            {
                "validate" => RunValidate(rest),
                "fix" => RunFix(rest),
                "split" => RunSplit(rest),
                "inspect" => RunInspect(rest),
                "audit" => RunAudit(rest),
                "export-pdf" => RunExportReport(rest, isPdf: true),
                "export-xlsx" => RunExportReport(rest, isPdf: false),
                "db-stats" => RunDbStats(rest),
                "db-history" => RunDbHistory(rest),
                _ => UnknownCommand(cmd)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erro: {ex.Message}");
            return 1;
        }
    }

    private static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Comando desconhecido: {cmd}");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine($"OpenSintegra Contábil CLI v{AppVersion} (Licença MIT)");
        Console.WriteLine("Uso: opensintegra-cli <comando> [opções]");
        Console.WriteLine();
        Console.WriteLine("Comandos:");
        Console.WriteLine("  validate <sintegra.txt> [--month YYYYMM] [--json] [--save-db] [--db path]");
        Console.WriteLine("  fix <entrada.txt> [-o saída.txt]");
        Console.WriteLine("  split <anual.txt> [-d pasta_saída]");
        Console.WriteLine("  inspect <sintegra.txt> --line <número>");
        Console.WriteLine("  audit <sintegra.txt> [--xml zip_ou_pasta] [--sped sped.txt] [--month YYYYMM] [--json]");
        Console.WriteLine("  export-pdf <sintegra.txt> [-o relatório.pdf] [--xml zip_ou_pasta] [--sped sped.txt] [--month YYYYMM] [--regime nome]");
        Console.WriteLine("  export-xlsx <sintegra.txt> [-o planilha.xlsx] [--xml zip_ou_pasta] [--sped sped.txt] [--month YYYYMM] [--regime nome]");
        Console.WriteLine("  db-stats [--db path] [--limit-mb N]");
        Console.WriteLine("  db-history [--cnpj 14digitos] [--db path]");
    }

    private static int RunValidate(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("Informe o caminho do arquivo Sintegra (.txt).");

        string path = args[0];
        bool asJson = HasFlag(args, "--json");
        bool saveDb = HasFlag(args, "--save-db");
        string? month = GetOption(args, "--month");
        string? dbPath = GetOption(args, "--db");

        byte[] bytes = File.ReadAllBytes(path);
        var doc = SintegraParser.ParseBytes(bytes);
        var report = SintegraValidator.Validate(doc);
        var scoped = PeriodFilter.CreateScopedView(doc, report, null, month);

        if (saveDb && doc.Reg10 is not null)
        {
            var db = new FiscalDatabase(dbPath);
            db.RecordImport(doc, report, null, Path.GetFileName(path));
        }

        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                file = Path.GetFileName(path),
                summary = report.Summary,
                scopedPeriod = scoped.PeriodLabel,
                scopedEntradasCents = scoped.TotalEntradasCents,
                scopedSaidasCents = scoped.TotalSaidasCents,
                monthlyBalance = scoped.MonthlyRows,
                diagnostics = scoped.Diagnostics.Take(200)
            }, JsonOpts));
            return report.Summary.Valid ? 0 : 1;
        }

        Console.WriteLine($"Arquivo: {Path.GetFileName(path)} ({report.Summary.TotalLines} linhas, {report.Summary.ByteSize} bytes)");
        Console.WriteLine($"CRC32: {report.Summary.Crc32Hex} | SHA256: {report.Summary.Sha256Hex}");
        if (doc.Reg10 is { } r10)
            Console.WriteLine($"Empresa: {r10.NomeContribuinte.Trim()} | CNPJ: {TaxIdValidator.FormatCnpj(r10.Cnpj)} | IE: {r10.Ie.Trim()}");
        Console.WriteLine($"Escopo: {scoped.PeriodLabel}");
        Console.WriteLine($"Resultado: {(report.Summary.Valid ? "VÁLIDO" : "INVÁLIDO")} (Erros: {report.Summary.ErrorCount}, Avisos: {report.Summary.WarningCount})");
        Console.WriteLine($"Entradas: R$ {TaxIdValidator.FormatCentsBrl(scoped.TotalEntradasCents)} (Compras R$ {TaxIdValidator.FormatCentsBrl(scoped.ComprasEntradasCents)})");
        Console.WriteLine($"Saídas:   R$ {TaxIdValidator.FormatCentsBrl(scoped.TotalSaidasCents)}");
        Console.WriteLine($"Saldo (Saídas - Entradas): {TaxIdValidator.FormatSignedCentsBrl(scoped.SaldoLiquidoCents)}");

        if (scoped.Diagnostics.Count > 0)
        {
            Console.WriteLine("\nDiagnósticos (primeiros 25):");
            foreach (var d in scoped.Diagnostics.Take(25))
                Console.WriteLine($"  [{d.SeverityLabel}] {d.Code} L{d.Line} ({d.RecordType}): {d.Message}");
        }

        return report.Summary.Valid ? 0 : 1;
    }

    private static int RunFix(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("Informe o arquivo Sintegra de entrada.");

        string inPath = args[0];
        string outPath = GetOption(args, "-o") ??
                         Path.Combine(Path.GetDirectoryName(Path.GetFullPath(inPath)) ?? ".",
                             Path.GetFileNameWithoutExtension(inPath) + "_CORRIGIDO.txt");

        byte[] raw = File.ReadAllBytes(inPath);
        var fx = SintegraAutoFixer.AutoFix(raw, new FixOptions());
        File.WriteAllBytes(outPath, fx.FixedBytes);

        Console.WriteLine($"Correção aplicada e salva em: {outPath}");
        Console.WriteLine($"Erros:  {fx.BeforeSummary.ErrorCount} -> {fx.AfterSummary.ErrorCount}");
        Console.WriteLine($"Avisos: {fx.BeforeSummary.WarningCount} -> {fx.AfterSummary.WarningCount}");
        foreach (var af in fx.AppliedFixes)
            Console.WriteLine($"  [{af.Code}] {af.Description} ({af.AffectedCount} registro(s))");

        return fx.AfterSummary.Valid ? 0 : 1;
    }

    private static int RunSplit(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("Informe o arquivo Sintegra anual.");

        string inPath = args[0];
        string outDir = GetOption(args, "-d") ??
                        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(inPath)) ?? ".", "sintegra_mensal");
        Directory.CreateDirectory(outDir);

        byte[] raw = File.ReadAllBytes(inPath);
        var splits = SintegraAutoFixer.SplitAnnualByMonth(raw);
        foreach (var sp in splits)
        {
            string target = Path.Combine(outDir, sp.SuggestedFileName);
            File.WriteAllBytes(target, sp.FileBytes);
            Console.WriteLine($"  {sp.MonthLabel}: {sp.SuggestedFileName} ({sp.LineCount} linhas, Erros: {sp.Summary.ErrorCount}, Avisos: {sp.Summary.WarningCount})");
        }
        Console.WriteLine($"Total de {splits.Count} arquivo(s) mensal(is) gerado(s) em: {outDir}");
        return 0;
    }

    private static int RunInspect(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("Informe o arquivo Sintegra.");

        string path = args[0];
        int lineNum = int.Parse(GetOption(args, "--line") ?? "1");

        var doc = SintegraParser.ParseBytes(File.ReadAllBytes(path));
        if (lineNum < 1 || lineNum > doc.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineNum), $"Linha {lineNum} fora do intervalo 1..{doc.Lines.Count}.");

        var rawLine = doc.Lines[lineNum - 1];
        Console.WriteLine($"Linha {rawLine.LineNumber} | Registro {rawLine.RecordType} | Comprimento {rawLine.CharLen} | CRLF: {rawLine.HasCr && rawLine.HasLf}");
        Console.WriteLine(rawLine.RawText);
        Console.WriteLine();

        foreach (var f in RecordSchemaDecomposer.DecomposeLine(rawLine.RawText))
            Console.WriteLine($"  Col {f.PositionRange} | {f.Name,-35} | Bruto='{f.RawValue}' | Formatado={f.FormattedValue}");

        return 0;
    }

    private static int RunAudit(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("Informe o arquivo Sintegra.");

        string sintPath = args[0];
        string? xmlPath = GetOption(args, "--xml");
        string? spedPath = GetOption(args, "--sped");
        string? month = GetOption(args, "--month");
        bool asJson = HasFlag(args, "--json");

        var doc = SintegraParser.ParseBytes(File.ReadAllBytes(sintPath));
        var report = SintegraValidator.Validate(doc);
        var xmls = !string.IsNullOrEmpty(xmlPath) ? FiscalCrossAuditor.LoadXmlsFromPath(xmlPath) : new List<XmlDocumentSummary>();
        var sped = !string.IsNullOrEmpty(spedPath) ? FiscalCrossAuditor.ParseSpedFiscalFile(spedPath) : null;
        var audit = FiscalCrossAuditor.RunCrossAudit(doc, xmls, sped);
        var scoped = PeriodFilter.CreateScopedView(doc, report, audit, month);

        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                precisionScorePct = audit.PrecisionScorePct,
                period = scoped.PeriodLabel,
                xmlComprasTotal = scoped.XmlComprasTotal,
                xmlComprasLancadas = scoped.XmlComprasLancadas,
                xmlComprasOmissas = scoped.XmlComprasOmissas,
                xmlComprasOmissasCents = scoped.XmlComprasOmissasCents,
                monthlyCoverage = audit.MonthlyCoverage,
                findings = audit.Findings.Take(200)
            }, JsonOpts));
            return 0;
        }

        Console.WriteLine($"Auditoria Cruzada ({scoped.PeriodLabel}) - Precisão Global: {audit.PrecisionScorePct:N1}%");
        Console.WriteLine($"Compras XML: {scoped.XmlComprasLancadas} lançadas / {scoped.XmlComprasTotal} totais ({scoped.XmlComprasOmissas} omissas = R$ {TaxIdValidator.FormatCentsBrl(scoped.XmlComprasOmissasCents)})");
        foreach (var gap in scoped.XmlDocumentRows.Where(r => r.IsCriticalGap))
            Console.WriteLine($"  [OMISSA] {gap.DataBr} NF {gap.Numero}/{gap.Serie} | {gap.EmitNome} | R$ {TaxIdValidator.FormatCentsBrl(gap.XmlValorCents)}");

        return 0;
    }

    private static int RunExportReport(string[] args, bool isPdf)
    {
        if (args.Length == 0)
            throw new ArgumentException("Informe o arquivo Sintegra.");

        string sintPath = args[0];
        string? xmlPath = GetOption(args, "--xml");
        string? spedPath = GetOption(args, "--sped");
        string? month = GetOption(args, "--month");
        string regime = GetOption(args, "--regime") ?? "Simples Nacional";
        string ext = isPdf ? ".pdf" : ".xlsx";
        string outPath = GetOption(args, "-o") ??
                         Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sintPath)) ?? ".",
                             Path.GetFileNameWithoutExtension(sintPath) + "_Relatorio" + ext);

        var doc = SintegraParser.ParseBytes(File.ReadAllBytes(sintPath));
        var report = SintegraValidator.Validate(doc);
        AuditReport? audit = null;
        if (!string.IsNullOrEmpty(xmlPath) || !string.IsNullOrEmpty(spedPath))
        {
            var xmls = !string.IsNullOrEmpty(xmlPath) ? FiscalCrossAuditor.LoadXmlsFromPath(xmlPath) : new List<XmlDocumentSummary>();
            var sped = !string.IsNullOrEmpty(spedPath) ? FiscalCrossAuditor.ParseSpedFiscalFile(spedPath) : null;
            audit = FiscalCrossAuditor.RunCrossAudit(doc, xmls, sped);
        }

        var scoped = PeriodFilter.CreateScopedView(doc, report, audit, month);
        var ctx = new FiscalReportContext(doc, report, scoped, audit, regime, AppVersion);

        if (isPdf)
            PdfReportGenerator.GeneratePdf(ctx, outPath);
        else
            ExcelReportGenerator.GenerateWorkbook(ctx, outPath);

        Console.WriteLine($"Relatório {(isPdf ? "PDF" : "Excel (.xlsx)")} gerado com sucesso: {outPath}");
        return 0;
    }

    private static int RunDbStats(string[] args)
    {
        string? dbPath = GetOption(args, "--db");
        string? limitStr = GetOption(args, "--limit-mb");
        var db = new FiscalDatabase(dbPath);
        if (long.TryParse(limitStr, out long mb))
            db.UpdateSettings(mb, db.GetStorageStats().AutoPrune);

        var stats = db.GetStorageStats();
        Console.WriteLine($"Base SQLite: {stats.DbPath}");
        Console.WriteLine($"Uso:         {stats.FormattedSummary} ({stats.UsageFraction * 100.0:N1}%)");
        Console.WriteLine($"Auto-Prune:  {(stats.AutoPrune ? "Ativado" : "Desativado")}");
        return 0;
    }

    private static int RunDbHistory(string[] args)
    {
        string? dbPath = GetOption(args, "--db");
        string? cnpj = GetOption(args, "--cnpj");
        var db = new FiscalDatabase(dbPath);

        var companies = db.GetCompanies();
        Console.WriteLine($"Empresas registradas: {companies.Count}");
        foreach (var c in companies)
            Console.WriteLine($"  {TaxIdValidator.FormatCnpj(c.Cnpj)} - {c.RazaoSocial} ({c.TaxRegime})");

        if (!string.IsNullOrWhiteSpace(cnpj))
        {
            var timeline = db.GetCompanyMonthlyTimeline(cnpj);
            Console.WriteLine($"\nHistórico mensal ({timeline.Count} competência(s)):");
            foreach (var m in timeline)
                Console.WriteLine($"  {m.MonthLabel}: Entradas R$ {TaxIdValidator.FormatCentsBrl(m.TotalEntradasCents)} | Saídas R$ {TaxIdValidator.FormatCentsBrl(m.TotalSaidasCents)} | Arquivo: {m.SourceFileName}");
        }
        return 0;
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetOption(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}
