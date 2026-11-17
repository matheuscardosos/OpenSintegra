using Microsoft.Data.Sqlite;
using OpenSintegra.Core;

namespace OpenSintegra.Data;

public sealed record DatabaseStorageStats(
    string DbPath,
    long UsedBytes,
    long MaxLimitBytes,
    long MaxLimitMb,
    bool AutoPrune,
    double UsageFraction,
    string FormattedSummary
);

public sealed record CompanyRecord(
    long Id,
    string Cnpj,
    string Ie,
    string RazaoSocial,
    string Municipio,
    string Uf,
    string TaxRegime,
    string UpdatedAt
);

public sealed record ImportHistoryRecord(
    long Id,
    long CompanyId,
    string Cnpj,
    string RazaoSocial,
    string FileName,
    string Sha256,
    string Crc32,
    string PeriodStart,
    string PeriodEnd,
    bool IsAnnual,
    int DistinctMonths,
    int TotalLines,
    int ErrorCount,
    int WarningCount,
    long EntradasCents,
    long ComprasCents,
    long SaidasCents,
    string? XmlPackageName,
    int XmlComprasTotal,
    int XmlComprasOmissas,
    long XmlOmissasCents,
    string ImportedAt
);

public sealed record CompanyMonthlyHistoryRow(
    string YearMonth,
    string MonthLabel,
    long ComprasCents,
    long OutrasEntradasCents,
    long TotalEntradasCents,
    long TotalSaidasCents,
    long SaldoCents,
    double? RatioPct,
    string SourceFileName,
    string ImportedAt
);

/// <summary>
/// Thread-safe SQLite repository storing company tax metadata, Sintegra import history,
/// monthly financial snapshots and user-configurable database size limits.
/// </summary>
public sealed class FiscalDatabase
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly object _syncLock = new();

    public FiscalDatabase(string? dbPath = null)
    {
        _dbPath = dbPath ?? GetDefaultDbPath();
        string? dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        InitializeSchema();
    }

    public static string GetDefaultDbPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "OpenSintegra", "opensintegra.db");
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    private void InitializeSchema()
    {
        lock (_syncLock)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS app_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS companies (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    cnpj TEXT NOT NULL UNIQUE,
                    ie TEXT NOT NULL,
                    razao_social TEXT NOT NULL,
                    municipio TEXT NOT NULL,
                    uf TEXT NOT NULL,
                    tax_regime TEXT NOT NULL DEFAULT 'Simples Nacional',
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS import_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    company_id INTEGER NOT NULL,
                    file_name TEXT NOT NULL,
                    sha256 TEXT NOT NULL,
                    crc32 TEXT NOT NULL,
                    period_start TEXT NOT NULL,
                    period_end TEXT NOT NULL,
                    is_annual INTEGER NOT NULL,
                    distinct_months INTEGER NOT NULL,
                    total_lines INTEGER NOT NULL,
                    error_count INTEGER NOT NULL,
                    warning_count INTEGER NOT NULL,
                    entradas_cents INTEGER NOT NULL,
                    compras_cents INTEGER NOT NULL,
                    saidas_cents INTEGER NOT NULL,
                    icms_entradas_cents INTEGER NOT NULL,
                    icms_saidas_cents INTEGER NOT NULL,
                    xml_package_name TEXT,
                    xml_compras_total INTEGER NOT NULL DEFAULT 0,
                    xml_compras_omissas INTEGER NOT NULL DEFAULT 0,
                    xml_omissas_cents INTEGER NOT NULL DEFAULT 0,
                    imported_at TEXT NOT NULL,
                    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS monthly_snapshots (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    import_id INTEGER NOT NULL,
                    year_month TEXT NOT NULL,
                    compras_cents INTEGER NOT NULL,
                    outras_entradas_cents INTEGER NOT NULL,
                    total_entradas_cents INTEGER NOT NULL,
                    saidas_reg50_cents INTEGER NOT NULL,
                    saidas_reg61_cents INTEGER NOT NULL,
                    total_saidas_cents INTEGER NOT NULL,
                    saldo_cents INTEGER NOT NULL,
                    ratio_pct REAL,
                    xml_compras_total INTEGER NOT NULL DEFAULT 0,
                    xml_compras_omissas INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (import_id) REFERENCES import_history(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS audit_missing_docs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    import_id INTEGER NOT NULL,
                    year_month TEXT NOT NULL,
                    doc_date TEXT NOT NULL,
                    emit_cnpj TEXT NOT NULL,
                    emit_nome TEXT NOT NULL,
                    doc_num TEXT NOT NULL,
                    category TEXT NOT NULL,
                    valor_cents INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    FOREIGN KEY (import_id) REFERENCES import_history(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_import_company_date ON import_history(company_id, imported_at DESC);
                CREATE INDEX IF NOT EXISTS idx_snapshots_import ON monthly_snapshots(import_id, year_month);

                INSERT OR IGNORE INTO app_settings(key, value) VALUES('max_db_size_mb', '1024');
                INSERT OR IGNORE INTO app_settings(key, value) VALUES('auto_prune_oldest', '1');
                """;
            cmd.ExecuteNonQuery();
        }
    }

    public string? GetSetting(string key)
    {
        lock (_syncLock)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_settings WHERE key = $k;";
            cmd.Parameters.AddWithValue("$k", key);
            return cmd.ExecuteScalar() as string;
        }
    }

    public void SetSetting(string key, string? value)
    {
        lock (_syncLock)
        {
            using var conn = OpenConnection();
            if (string.IsNullOrEmpty(value))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM app_settings WHERE key = $k;";
                cmd.Parameters.AddWithValue("$k", key);
                cmd.ExecuteNonQuery();
            }
            else
            {
                UpsertSetting(conn, key, value);
            }
        }
    }

    public DatabaseStorageStats GetStorageStats()
    {
        lock (_syncLock)
        {
            long maxMb = ReadLongSetting("max_db_size_mb", 1024L);
            bool autoPrune = ReadStringSetting("auto_prune_oldest", "1") == "1";

            long bytes = 0;
            if (File.Exists(_dbPath))
                bytes += new FileInfo(_dbPath).Length;
            if (File.Exists(_dbPath + "-wal"))
                bytes += new FileInfo(_dbPath + "-wal").Length;

            long maxBytes = Math.Max(10L, maxMb) * 1024L * 1024L;
            double frac = Math.Clamp(bytes / (double)maxBytes, 0.0, 1.0);
            string summary = $"Base fiscal: {FormatBytes(bytes)} de {FormatLimit(maxMb)}";

            return new DatabaseStorageStats(_dbPath, bytes, maxBytes, maxMb, autoPrune, frac, summary);
        }
    }

    public void UpdateSettings(long maxLimitMb, bool autoPrune)
    {
        long clampedMb = Math.Clamp(maxLimitMb, 10L, 102400L);
        lock (_syncLock)
        {
            using var conn = OpenConnection();
            UpsertSetting(conn, "max_db_size_mb", clampedMb.ToString());
            UpsertSetting(conn, "auto_prune_oldest", autoPrune ? "1" : "0");
            EnforceQuotaInternal(conn, clampedMb * 1024L * 1024L, autoPrune);
        }
    }

    public string GetCompanyTaxRegime(string cnpj)
    {
        lock (_syncLock)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT tax_regime FROM companies WHERE cnpj = $cnpj;";
            cmd.Parameters.AddWithValue("$cnpj", cnpj.Trim());
            return cmd.ExecuteScalar() as string ?? "Simples Nacional";
        }
    }

    public void UpdateCompanyTaxRegime(string cnpj, string taxRegime)
    {
        lock (_syncLock)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE companies SET tax_regime = $reg, updated_at = $now WHERE cnpj = $cnpj;";
            cmd.Parameters.AddWithValue("$reg", taxRegime.Trim());
            cmd.Parameters.AddWithValue("$now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("$cnpj", cnpj.Trim());
            cmd.ExecuteNonQuery();
        }
    }

    public long RecordImport(
        SintegraDocument doc,
        ValidationReport report,
        AuditReport? audit,
        string fileName,
        string? xmlPackageName = null)
    {
        if (doc.Reg10 is null) return 0;
        var r10 = doc.Reg10;
        var sum = report.Summary;
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        lock (_syncLock)
        {
            using var conn = OpenConnection();
            using var tx = conn.BeginTransaction();

            long companyId;
            using (var upComp = conn.CreateCommand())
            {
                upComp.Transaction = tx;
                upComp.CommandText = """
                    INSERT INTO companies (cnpj, ie, razao_social, municipio, uf, updated_at)
                    VALUES ($cnpj, $ie, $nome, $mun, $uf, $now)
                    ON CONFLICT(cnpj) DO UPDATE SET
                        ie = excluded.ie,
                        razao_social = excluded.razao_social,
                        municipio = excluded.municipio,
                        uf = excluded.uf,
                        updated_at = excluded.updated_at;
                    SELECT id FROM companies WHERE cnpj = $cnpj;
                    """;
                upComp.Parameters.AddWithValue("$cnpj", r10.Cnpj.Trim());
                upComp.Parameters.AddWithValue("$ie", r10.Ie.Trim());
                upComp.Parameters.AddWithValue("$nome", r10.NomeContribuinte.Trim());
                upComp.Parameters.AddWithValue("$mun", r10.Municipio.Trim());
                upComp.Parameters.AddWithValue("$uf", r10.Uf.Trim());
                upComp.Parameters.AddWithValue("$now", now);
                companyId = Convert.ToInt64(upComp.ExecuteScalar() ?? 0);
            }

            // Avoid duplicate rows if the exact same file SHA256 was already recorded for this company
            using (var delDup = conn.CreateCommand())
            {
                delDup.Transaction = tx;
                delDup.CommandText = "DELETE FROM import_history WHERE company_id = $cid AND sha256 = $sha;";
                delDup.Parameters.AddWithValue("$cid", companyId);
                delDup.Parameters.AddWithValue("$sha", sum.Sha256Hex);
                delDup.ExecuteNonQuery();
            }

            long importId;
            using (var insImp = conn.CreateCommand())
            {
                insImp.Transaction = tx;
                insImp.CommandText = """
                    INSERT INTO import_history (
                        company_id, file_name, sha256, crc32, period_start, period_end,
                        is_annual, distinct_months, total_lines, error_count, warning_count,
                        entradas_cents, compras_cents, saidas_cents, icms_entradas_cents, icms_saidas_cents,
                        xml_package_name, xml_compras_total, xml_compras_omissas, xml_omissas_cents, imported_at
                    ) VALUES (
                        $cid, $fn, $sha, $crc, $ps, $pe,
                        $ann, $dm, $tl, $err, $warn,
                        $ent, $comp, $sai, $icmsE, $icmsS,
                        $xpkg, $xct, $xco, $xoc, $now
                    );
                    SELECT last_insert_rowid();
                    """;
                insImp.Parameters.AddWithValue("$cid", companyId);
                insImp.Parameters.AddWithValue("$fn", Path.GetFileName(fileName));
                insImp.Parameters.AddWithValue("$sha", sum.Sha256Hex);
                insImp.Parameters.AddWithValue("$crc", sum.Crc32Hex);
                insImp.Parameters.AddWithValue("$ps", r10.DataInicial);
                insImp.Parameters.AddWithValue("$pe", r10.DataFinal);
                insImp.Parameters.AddWithValue("$ann", sum.IsMultiMonthPeriod ? 1 : 0);
                insImp.Parameters.AddWithValue("$dm", sum.DistinctMonthsCount);
                insImp.Parameters.AddWithValue("$tl", sum.TotalLines);
                insImp.Parameters.AddWithValue("$err", sum.ErrorCount);
                insImp.Parameters.AddWithValue("$warn", sum.WarningCount);
                insImp.Parameters.AddWithValue("$ent", sum.ValorContabilEntradasCents);
                insImp.Parameters.AddWithValue("$comp", sum.ComprasEntradasCents);
                insImp.Parameters.AddWithValue("$sai", sum.ValorContabilSaidasCents);
                insImp.Parameters.AddWithValue("$icmsE", sum.ValorIcmsEntradasCents);
                insImp.Parameters.AddWithValue("$icmsS", sum.ValorIcmsSaidasCents);
                insImp.Parameters.AddWithValue("$xpkg", xmlPackageName is null ? DBNull.Value : Path.GetFileName(xmlPackageName));
                insImp.Parameters.AddWithValue("$xct", audit?.XmlComprasTotal ?? 0);
                insImp.Parameters.AddWithValue("$xco", audit?.XmlComprasOmissas ?? 0);
                insImp.Parameters.AddWithValue("$xoc", audit?.XmlComprasOmissasCents ?? 0L);
                insImp.Parameters.AddWithValue("$now", now);
                importId = Convert.ToInt64(insImp.ExecuteScalar() ?? 0);
            }

            var covByYm = audit?.MonthlyCoverage.ToDictionary(c => c.YearMonth, StringComparer.Ordinal)
                          ?? new Dictionary<string, MonthlyXmlCoverageRow>(StringComparer.Ordinal);

            foreach (var mb in report.MonthlyBalance)
            {
                covByYm.TryGetValue(mb.YearMonth, out var cov);
                using var insSnap = conn.CreateCommand();
                insSnap.Transaction = tx;
                insSnap.CommandText = """
                    INSERT INTO monthly_snapshots (
                        import_id, year_month, compras_cents, outras_entradas_cents, total_entradas_cents,
                        saidas_reg50_cents, saidas_reg61_cents, total_saidas_cents, saldo_cents, ratio_pct,
                        xml_compras_total, xml_compras_omissas
                    ) VALUES (
                        $iid, $ym, $comp, $outr, $tent, $s50, $s61, $tsai, $sal, $rat, $xct, $xco
                    );
                    """;
                insSnap.Parameters.AddWithValue("$iid", importId);
                insSnap.Parameters.AddWithValue("$ym", mb.YearMonth);
                insSnap.Parameters.AddWithValue("$comp", mb.ComprasEntradasCents);
                insSnap.Parameters.AddWithValue("$outr", mb.OutrasEntradasCents);
                insSnap.Parameters.AddWithValue("$tent", mb.TotalEntradasCents);
                insSnap.Parameters.AddWithValue("$s50", mb.SaidasReg50Cents);
                insSnap.Parameters.AddWithValue("$s61", mb.SaidasReg61Cents);
                insSnap.Parameters.AddWithValue("$tsai", mb.TotalSaidasCents);
                insSnap.Parameters.AddWithValue("$sal", mb.SaldoLiquidoCents);
                insSnap.Parameters.AddWithValue("$rat", mb.SaidaSobreEntradaPct.HasValue ? mb.SaidaSobreEntradaPct.Value : DBNull.Value);
                insSnap.Parameters.AddWithValue("$xct", cov?.XmlComprasTotal ?? 0);
                insSnap.Parameters.AddWithValue("$xco", cov?.XmlComprasOmissas ?? 0);
                insSnap.ExecuteNonQuery();
            }

            if (audit is not null)
            {
                foreach (var dr in audit.DocumentRows.Where(r => r.IsCriticalGap).Take(500))
                {
                    using var insDoc = conn.CreateCommand();
                    insDoc.Transaction = tx;
                    insDoc.CommandText = """
                        INSERT INTO audit_missing_docs (
                            import_id, year_month, doc_date, emit_cnpj, emit_nome, doc_num, category, valor_cents, status
                        ) VALUES ($iid, $ym, $dt, $ec, $en, $dn, $cat, $val, $st);
                        """;
                    insDoc.Parameters.AddWithValue("$iid", importId);
                    insDoc.Parameters.AddWithValue("$ym", dr.YearMonth);
                    insDoc.Parameters.AddWithValue("$dt", dr.DataBr);
                    insDoc.Parameters.AddWithValue("$ec", dr.EmitCnpj);
                    insDoc.Parameters.AddWithValue("$en", dr.EmitNome);
                    insDoc.Parameters.AddWithValue("$dn", dr.Numero);
                    insDoc.Parameters.AddWithValue("$cat", dr.CategoryLabel);
                    insDoc.Parameters.AddWithValue("$val", dr.XmlValorCents);
                    insDoc.Parameters.AddWithValue("$st", dr.MatchStatus);
                    insDoc.ExecuteNonQuery();
                }
            }

            tx.Commit();

            long maxMb = ReadLongSetting("max_db_size_mb", 1024L);
            bool autoPrune = ReadStringSetting("auto_prune_oldest", "1") == "1";
            EnforceQuotaInternal(conn, maxMb * 1024L * 1024L, autoPrune);

            return importId;
        }
    }

    public IReadOnlyList<CompanyRecord> GetCompanies()
    {
        lock (_syncLock)
        {
            var list = new List<CompanyRecord>();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, cnpj, ie, razao_social, municipio, uf, tax_regime, updated_at FROM companies ORDER BY updated_at DESC;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new CompanyRecord(
                    rd.GetInt64(0), rd.GetString(1), rd.GetString(2),
                    rd.GetString(3), rd.GetString(4), rd.GetString(5),
                    rd.GetString(6), rd.GetString(7)));
            }
            return list;
        }
    }

    public IReadOnlyList<ImportHistoryRecord> GetImportHistory(string? cnpjFilter = null, int limit = 200)
    {
        lock (_syncLock)
        {
            var list = new List<ImportHistoryRecord>();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT h.id, h.company_id, c.cnpj, c.razao_social, h.file_name, h.sha256, h.crc32,
                       h.period_start, h.period_end, h.is_annual, h.distinct_months, h.total_lines,
                       h.error_count, h.warning_count, h.entradas_cents, h.compras_cents, h.saidas_cents,
                       h.xml_package_name, h.xml_compras_total, h.xml_compras_omissas, h.xml_omissas_cents, h.imported_at
                FROM import_history h
                JOIN companies c ON c.id = h.company_id
                WHERE ($cnpj IS NULL OR c.cnpj = $cnpj)
                ORDER BY h.imported_at DESC, h.id DESC
                LIMIT $lim;
                """;
            cmd.Parameters.AddWithValue("$cnpj", string.IsNullOrWhiteSpace(cnpjFilter) ? DBNull.Value : cnpjFilter.Trim());
            cmd.Parameters.AddWithValue("$lim", limit);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new ImportHistoryRecord(
                    rd.GetInt64(0), rd.GetInt64(1), rd.GetString(2), rd.GetString(3),
                    rd.GetString(4), rd.GetString(5), rd.GetString(6), rd.GetString(7), rd.GetString(8),
                    rd.GetInt64(9) == 1, rd.GetInt32(10), rd.GetInt32(11),
                    rd.GetInt32(12), rd.GetInt32(13), rd.GetInt64(14), rd.GetInt64(15), rd.GetInt64(16),
                    rd.IsDBNull(17) ? null : rd.GetString(17),
                    rd.GetInt32(18), rd.GetInt32(19), rd.GetInt64(20), rd.GetString(21)));
            }
            return list;
        }
    }

    public IReadOnlyList<CompanyMonthlyHistoryRow> GetCompanyMonthlyTimeline(string cnpj)
    {
        lock (_syncLock)
        {
            var list = new List<CompanyMonthlyHistoryRow>();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            // For each calendar month, return the most recently imported snapshot
            cmd.CommandText = """
                SELECT s.year_month, s.compras_cents, s.outras_entradas_cents, s.total_entradas_cents,
                       s.total_saidas_cents, s.saldo_cents, s.ratio_pct, h.file_name, h.imported_at
                FROM monthly_snapshots s
                JOIN import_history h ON h.id = s.import_id
                JOIN companies c ON c.id = h.company_id
                WHERE c.cnpj = $cnpj
                  AND s.id = (
                      SELECT s2.id
                      FROM monthly_snapshots s2
                      JOIN import_history h2 ON h2.id = s2.import_id
                      WHERE h2.company_id = c.id AND s2.year_month = s.year_month
                      ORDER BY h2.imported_at DESC, h2.id DESC
                      LIMIT 1
                  )
                ORDER BY s.year_month ASC;
                """;
            cmd.Parameters.AddWithValue("$cnpj", cnpj.Trim());
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                string ym = rd.GetString(0);
                list.Add(new CompanyMonthlyHistoryRow(
                    ym,
                    TaxIdValidator.FormatMonthLabel(ym),
                    rd.GetInt64(1),
                    rd.GetInt64(2),
                    rd.GetInt64(3),
                    rd.GetInt64(4),
                    rd.GetInt64(5),
                    rd.IsDBNull(6) ? null : rd.GetDouble(6),
                    rd.GetString(7),
                    rd.GetString(8)));
            }
            return list;
        }
    }

    public void DeleteImport(long importId)
    {
        lock (_syncLock)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM import_history WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", importId);
            cmd.ExecuteNonQuery();
            CompactInternal(conn);
        }
    }

    public void ClearAllHistory()
    {
        lock (_syncLock)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM import_history; DELETE FROM companies;";
            cmd.ExecuteNonQuery();
            CompactInternal(conn);
        }
    }

    public void CompactDatabase()
    {
        lock (_syncLock)
        {
            using var conn = OpenConnection();
            CompactInternal(conn);
        }
    }

    private void EnforceQuotaInternal(SqliteConnection conn, long maxBytes, bool autoPrune)
    {
        if (!autoPrune || maxBytes <= 0) return;

        long curSize = GetFileBytes();
        if (curSize <= maxBytes) return;

        // Prune oldest imports until within 85% of quota
        long targetBytes = (long)(maxBytes * 0.85);
        for (int pass = 0; pass < 50 && GetFileBytes() > targetBytes; pass++)
        {
            using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM import_history WHERE id = (SELECT id FROM import_history ORDER BY imported_at ASC, id ASC LIMIT 1);";
            int affected = delCmd.ExecuteNonQuery();
            if (affected == 0) break;
            CompactInternal(conn);
        }
    }

    private long GetFileBytes()
    {
        long bytes = 0;
        if (File.Exists(_dbPath)) bytes += new FileInfo(_dbPath).Length;
        if (File.Exists(_dbPath + "-wal")) bytes += new FileInfo(_dbPath + "-wal").Length;
        return bytes;
    }

    private static void CompactInternal(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); VACUUM;";
        cmd.ExecuteNonQuery();
    }

    private long ReadLongSetting(string key, long fallback)
    {
        string raw = ReadStringSetting(key, fallback.ToString());
        return long.TryParse(raw, out long v) ? v : fallback;
    }

    private string ReadStringSetting(string key, string fallback)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = $k;";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string ?? fallback;
    }

    private static void UpsertSetting(SqliteConnection conn, string key, string value)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO app_settings(key, value) VALUES($k, $v) ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:N1} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:N1} MB";
        double gb = mb / 1024.0;
        return $"{gb:N2} GB";
    }

    public static string FormatLimit(long mb)
    {
        return mb >= 1024 && mb % 1024 == 0 ? $"{mb / 1024} GB" : $"{mb} MB";
    }
}
