# AGENTS.md - Engineering Standards & Agent Guidelines

This document defines mandatory architectural rules, coding conventions, domain invariants, and verification workflows for engineers and AI coding agents contributing to **OpenSintegra**.

## 1. Repository Layout & Dependency Boundaries

Projects follow strict unidirectional dependencies:

1. `OpenSintegra.Core` (`net8.0`, zero external NuGet dependencies):
   - Pure domain models, fixed-width parser, Convenio ICMS 57/95 validator, auto-fixer, monthly splitter, XML/SPED cross-auditor, and period scoper.
   - **Rule:** NEVER add UI frameworks (`Avalonia`), database drivers (`Sqlite`), or reporting dependencies (`SkiaSharp`, `ClosedXML`) to `OpenSintegra.Core`.
   - **Rule:** Long-running operations (`SintegraParser.ParseBytes`, `FiscalCrossAuditor.LoadXmlsFromPath`) must accept optional `IProgress<(int Percent, string Status)>?` and `CancellationToken` to keep UI and CLI callers responsive.

2. `OpenSintegra.Data` (`net8.0`, depends on `OpenSintegra.Core` and `Microsoft.Data.Sqlite`):
   - SQLite WAL persistence, company tax regime metadata, multi-month timeline aggregation, and storage quota enforcement (`max_db_size_mb`, `auto_prune`, `CompactDatabase()`).
   - **Rule:** All DB operations must synchronize cleanly (`_syncLock`) and never leave uncommitted transactions.

3. `OpenSintegra.Reports` (`net8.0`, depends on `OpenSintegra.Core`, `SkiaSharp`, `ClosedXML`):
   - Vector PDF generation (`PdfReportGenerator`) and formatted multi-sheet Excel workbooks (`ExcelReportGenerator`).
   - **Rule:** Keep all export libraries strictly permissive/MIT-compatible. Do NOT introduce AGPL or commercial-licensed dependencies.

4. `OpenSintegra.App` (`net8.0`, Avalonia 11 UI, depends on `Core`, `Data`, `Reports`):
   - Cross-platform desktop application (`win-x64`, `linux-x64`, `osx-arm64`).
   - **Rule:** Never block the Avalonia UI thread during file I/O, ZIP parsing, or validation. Run work via `Task.Run` inside `ImportProgressWindow`.
   - **Rule:** Never pass raw user file arguments directly into `BuildAvaloniaApp().StartWithClassicDesktopLifetime(...)`; capture them in `Program.Main` (`App.InitialFilePath`, `App.InitialAuditXmlPath`) and pass `Array.Empty<string>()`.
   - **Rule:** Use pure-code custom controls (`MonthlyBarChartControl`, `CompositionDonutControl`) or built-in Avalonia primitives (`Grid`, `Border`, `ScrollViewer`) rather than fragile XAML datagrids.

5. `OpenSintegra.Cli` (`net8.0`, depends on `Core`, `Data`, `Reports`):
   - Automation-friendly console entrypoint returning standard exit codes (`0` valid/success, `1` validation/domain error, `2` CLI argument error).

## 2. Fiscal Domain Invariants (DO NOT BREAK)

Agents modifying `OpenSintegra.Core` must preserve every invariant below:

1. **126-Column Record Width & CRLF:**
   - Every Sintegra record line is 126 ASCII/Latin1 characters terminated by `\r\n` (`CRLF`), except explicit state Reg 88 variants (e.g., `88SMS` 126..140 chars in MS).
   - Trailing space padding in `SintegraAutoFixer` MUST ONLY run on blank-padded trailing record types (`10`, `11`, `53`, `55`, `56`, `60`, `61`, `74`, `75`, `77`, `85`, `86`, `88`). Never space-pad fixed numeric suffix types (`50`, `51`, `54`, `70`, `76`).

2. **Record 75 Product Graph Completeness:**
   - Validating and pruning `Reg 75` products MUST inspect references from ALL product-bearing records: `Reg 54` (items except freight/discount pseudo-items `991`, `992`, `999`), `Reg 60D`, `Reg 60I`, `Reg 60R`, `Reg 61R`, `Reg 74`, `Reg 77`, `Reg 86`.
   - Dropping `Reg 75` items used by `Reg 61R` causes fatal validation error `2109`.

3. **Record 90 Canonical Rules:**
   - Do NOT include types `10` or `11` inside Record 90 totalizers (causes warning `1014`).
   - Do NOT emit zero-count entries (`00000000`) inside Record 90 (causes error `2192`).
   - Always place counter `99` (physical total file lines including all `90` lines) as the final entry.

4. **Record 54 Ordering:**
   - Contiguous `Reg 54` blocks belonging to the same `CNPJ|Modelo|Serie|Numero` must be sorted ascending by `NumItem` (`001`..`990`, then special codes `991`/`992`/`999`).

5. **Integer Cents Arithmetic:**
   - All monetary values use `long` integer cents (`ValorTotalCents`, `BaseIcmsCents`, `ValorIcmsCents`). Never convert monetary totals to `double`/`float` prior to final percentage/display formatting.

6. **Cross-Audit Accuracy:**
   - NF numbers exceeding 6 digits in XML (7+ digits) map onto Sintegra's 6-digit field via `numero % 1_000_000`.
   - Distinguish `XmlOperationCategory.CompraComercial` from non-purchase XMLs (`Bonificacao` 5910, `Comodato` 5908, returned purchases referenced via `tpNF=0` + `refNFe`, cancelled events) so false-positive omission alerts are avoided.

## 3. Coding Style & Hygiene

- Target `.NET 8`, `C# 12` (`Nullable` enabled, `ImplicitUsings` enabled).
- Avoid em-dashes or en-dashes (Unicode U+2014 / U+2013) in code, comments, UI labels, and documentation; use standard ASCII hyphens (`-`).
- Keep UI text, error messages, and fiscal reports in clear Brazilian Portuguese matching standard Brazilian accounting terminology (`Entradas`, `Saidas`, `Saldo`, `Competencia`, `CFOP`, `ICMS`), while keeping C# symbols, namespaces, and API documentation in English.
- Zero hardcoded user paths or private taxpayer fixtures inside `src/` or `tests/`.

## 4. Mandatory Verification Checklist Before Commit/PR

Always run the following checks with zero failures before considering any change ready:

```bash
dotnet build OpenSintegra.sln -c Release
dotnet test OpenSintegra.sln -c Release
```

When modifying UI or report layout:
- Verify CLI commands (`validate`, `fix`, `split`, `export-pdf`, `export-xlsx`) execute cleanly with valid outputs.
- Verify no temporary output files (`.pdf`, `.xlsx`, `.db`, `.txt` fixtures) remain untracked in the repository root.
