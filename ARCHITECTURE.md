# Arquitetura do Sistema - OpenSintegra Contabil

Este documento detalha o desenho modular, fluxos de dados, decisoes tecnicas e limites entre camadas do **OpenSintegra Contabil**.

## 1. Visao Geral em Camadas

O sistema foi projetado sob separacao estrita de responsabilidades em cinco modulos principais:

```text
+-------------------------------------------------------------------+
|  Apresentacao: OpenSintegra.App (Avalonia)  |  OpenSintegra.Cli   |
+-------------------------------------------------------------------+
          |                              |                   |
          v                              v                   v
+--------------------+    +--------------------+    +---------------+
| OpenSintegra.Data  |    | OpenSintegra.Core  |    | OpenSintegra. |
| (SQLite Repository)|<---| (Fiscal Engine)    |--->| Reports (PDF  |
+--------------------+    +--------------------+    |  & XLSX)      |
                                                    +---------------+
```

## 2. Modulos e Responsabilidades

### 2.1 `OpenSintegra.Core` (Nucleo Fiscal Puro)

Nao possui dependencias externas alem da BCL do `.NET 8`. Opera integralmente em memoria sobre `ReadOnlySpan` / vetores `byte[]` com codificacao `ISO-8859-1` (`Latin1`).

- **`Models.cs`:** Records imutaveis representando linhas brutas (`RawLine`), registros estruturados (`Reg10`..`Reg90`), diagnosticos (`Diagnostic`), resumo CFOP (`CfopSummaryRow`), balanco mensal (`MonthlyBalanceRow`) e relatorios consolidados (`ValidationReport`).
- **`SintegraParser.cs`:** Faz a leitura streaming/sequencial byte a byte preservando flags exatas de terminacao `CR`/`LF`, calculo de `CRC32` e `SHA-256`, além de reportar progresso incremental e aceitar `CancellationToken`.
- **`SintegraValidator.cs`:** Executa validacao sintatica, estrutural (126 colunas, terminadores), cadastral (`ValidateCnpj`, `ValidateCpf`, `ValidateIe` para as 27 UFs), hierarquica (`50`/`54`/`75`/`90`), por produto (`75` x `54`/`60R`/`61R`/`74`) e gerencial (saldo mensal `Entradas` vs `Saidas`). Em arquivos multi-mes, gera diagnostico informativo sem abortar a analise.
- **`SintegraAutoFixer.cs`:** Corrige erros recuperaveis em memoria ou disco (`AutoFix(...)`) e implementa a divisao de arquivos anuais por competencia (`SplitAnnualByMonth(...)`).
- **`FiscalCrossAuditor.cs`:** Parser de XMLs fiscais (`.xml` solto, diretorio ou `.zip` em streaming) e SPED Fiscal `EFD ICMS/IPI` (Blocos `0200` e `C100`), cruzando chaves de acesso, numeros modulo `1.000.000`, `refNFe` de devolucao e natureza CFOP.
- **`PeriodFilter.cs`:** Constroi a projecao `ScopedFiscalView` para `"ALL"` (periodo inteiro) ou `"YYYYMM"` (mes especifico), alimentando igualmente tela principal, graficos, visualizacao de impressao, PDF e XLSX.
- **`RecordSchemaDecomposer.cs`:** Mapeia posicoes de colunas 1..126 de todos os tipos de registro para o Inspetor de Campos interativo.

### 2.2 `OpenSintegra.Data` (Persistencia SQLite)

Baseado em `Microsoft.Data.Sqlite` operando em modo `WAL` (`Write-Ahead Logging`) e `NORMAL` sync para alta performance de escrita com seguranca transacional.

Tabelas gerenciadas automaticamente na inicializacao (`FiscalDatabase`):
- `app_settings`: Configuracoes globais (`max_db_size_mb`, `auto_prune_oldest`).
- `companies`: Cadastro por `CNPJ`, `IE`, razao social, municipio/UF e regime tributario escolhido pelo usuario.
- `import_history`: Registro de cada importacao com hashes (`sha256`, `crc32`), periodo, contadores de erro/aviso, totais financeiros e resumo de confronto XML. Utiliza indice unico `(company_id, sha256)` para evitar duplicidade acidental do mesmo arquivo.
- `monthly_snapshots`: Retrato mensal extraido de cada importacao (permitindo montar a linha do tempo continua da empresa mesmo quando o contador importa um `.txt` por mes).
- `audit_missing_docs`: Documentos criticos omissos detectados no confronto XML.

Quando `auto_prune_oldest` esta habilitado e o arquivo `.db` ultrapassa `max_db_size_mb`, importacoes mais antigas sao removidas em lote com execucao de `PRAGMA wal_checkpoint(TRUNCATE)` e `VACUUM`.

### 2.3 `OpenSintegra.Reports` (Exportacao Documental)

- **`PdfReportGenerator.cs`:** Renderizador vetorial direto em `SkiaSharp.SKDocument.CreatePdf` em paginas A4 (`595 x 842 pt`) com paginacao automatica, cabecalho institucional, blocos KPI, tabelas zebradas e rodape numerado.
- **`ExcelReportGenerator.cs`:** Construtor de planilha multi-aba via `ClosedXML` gerando 7 abas (`Resumo Executivo`, `Movimentacao Mensal`, `Apuracao por CFOP`, `Notas Reg 50`, `Saidas NFC-e Reg 61`, `Confronto XML`, `Diagnostico`), utilizando formulas dinamicas (`SUM`) e formatos monetarios brasileiros (`R$ #,##0.00`).

### 2.4 `OpenSintegra.App` (Desktop UI Multiplataforma)

Desenvolvida em **Avalonia UI 11** usando Fluent Theme em paleta clara tradicional contabil, com:
- Barra de menus classica + Ribbon com icones vetoriais `StreamGeometry` (`VectorIcons.cs`).
- Arvore lateral colapsavel + area de trabalho orientada a competencia filtrada via `ComboBox`.
- Controles graficos proprios em `DrawingContext` (`MonthlyBarChartControl` com hover e destaque de mes selecionado; `CompositionDonutControl`).
- Dialogo modal assincrono nao bloqueante (`ImportProgressWindow`) para arquivos extensos e ZIPs grandes.
- Janela de pre-visualizacao em papel A4 com zoom e impressao/exportacao (`ReportPreviewWindow`).
- Inspetor binario/colunar (`LineInspectorWindow`) para diagnostico preciso de linhas de 126 colunas.
