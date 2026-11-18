# Arquitetura do Sistema - OpenSintegra Contábil

Este documento detalha o desenho modular, fluxos de dados, decisões técnicas e limites entre camadas do **OpenSintegra Contábil**.

## 1. Visão Geral em Camadas

O sistema foi projetado sob separação estrita de responsabilidades em cinco módulos principais:

```text
+-------------------------------------------------------------------+
|  Apresentação: OpenSintegra.App (Avalonia)  |  OpenSintegra.Cli   |
+-------------------------------------------------------------------+
          |                              |                   |
          v                              v                   v
+--------------------+    +--------------------+    +---------------+
| OpenSintegra.Data  |    | OpenSintegra.Core  |    | OpenSintegra. |
| (SQLite Repository)|<---| (Fiscal Engine)    |--->| Reports (PDF  |
+--------------------+    +--------------------+    |  & XLSX)      |
                                                    +---------------+
```

## 2. Módulos e Responsabilidades

### 2.1 `OpenSintegra.Core` (Núcleo Fiscal Puro)

Não possui dependências externas além da BCL do `.NET 8`. Opera integralmente em memória sobre `ReadOnlySpan` / vetores `byte[]` com codificação `ISO-8859-1` (`Latin1`).

- **`Models.cs`:** Records imutáveis representando linhas brutas (`RawLine`), registros estruturados (`Reg10`..`Reg90`), diagnósticos (`Diagnostic`), resumo CFOP (`CfopSummaryRow`), balanço mensal (`MonthlyBalanceRow`), ranking de produtos (`TopProductRow`) e relatórios consolidados (`ValidationReport`).
- **`SintegraParser.cs`:** Faz a leitura streaming/sequencial byte a byte preservando flags exatas de terminação `CR`/`LF`, cálculo de `CRC32` e `SHA-256`, além de reportar progresso incremental e aceitar `CancellationToken`.
- **`SintegraValidator.cs`:** Executa validação sintática, estrutural (126 colunas, terminadores), cadastral (`ValidateCnpj`, `ValidateCpf`, `ValidateIe` para as 27 UFs), hierárquica (`50`/`54`/`75`/`90`), por produto (`75` x `54`/`60R`/`61R`/`74`) e gerencial (totais mensais `Entradas` vs `Saídas`). Em arquivos multi-mês, gera diagnóstico informativo sem abortar a análise.
- **`SintegraAutoFixer.cs`:** Corrige erros recuperáveis em memória ou disco (`AutoFix(...)`) e implementa a divisão de arquivos anuais por competência (`SplitAnnualByMonth(...)`).
- **`SintegraXmlInjector.cs`:** Converte XMLs de compras omitidas em registros `50`, `54` e `75` padronizados em 126 colunas, ajustando o Registro `90` e mantendo compatibilidade integral com `AutoFix`.
- **`FiscalCrossAuditor.cs`:** Parser de XMLs fiscais (`.xml` solto, diretório ou `.zip` em streaming) e SPED Fiscal `EFD ICMS/IPI` (Blocos `0200` e `C100`), cruzando chaves de acesso, números módulo `1.000.000`, `refNFe` de devolução e natureza CFOP.
- **`PeriodFilter.cs`:** Constrói a projeção `ScopedFiscalView` para `"ALL"` (período inteiro) ou `"YYYYMM"` (mês específico), calculando entradas, saídas, saldo líquido (`Saídas - Entradas`), cobertura XML por mês e ranking de produtos mais vendidos para tela principal, visualização de impressão, PDF e XLSX.
- **`RecordSchemaDecomposer.cs`:** Mapeia posições de colunas 1..126 de todos os tipos de registro para o Inspetor de Campos interativo.

### 2.2 `OpenSintegra.Data` (Persistência SQLite)

Baseado em `Microsoft.Data.Sqlite` operando em modo `WAL` (`Write-Ahead Logging`) e `NORMAL` sync para alta performance de escrita com segurança transacional.

Tabelas gerenciadas automaticamente na inicialização (`FiscalDatabase`):
- `app_settings`: Configurações globais (`max_db_size_mb`, `auto_prune_oldest`, `last_sintegra_path`, `last_xml_path`).
- `companies`: Cadastro por `CNPJ`, `IE`, razão social, município/UF e regime tributário escolhido pelo usuário.
- `import_history`: Registro de cada importação com hashes (`sha256`, `crc32`), período, contadores de erro/aviso, totais financeiros e resumo de confronto XML. Utiliza índice único `(company_id, sha256)` para evitar duplicidade acidental do mesmo arquivo.
- `monthly_snapshots`: Retrato mensal extraído de cada importação (permitindo montar a linha do tempo contínua da empresa mesmo quando o contador importa um `.txt` por mês).
- `audit_missing_docs`: Documentos críticos omissos detectados no confronto XML.

Quando `auto_prune_oldest` está habilitado e o arquivo `.db` ultrapassa `max_db_size_mb`, importações mais antigas são removidas em lote com execução de `PRAGMA wal_checkpoint(TRUNCATE)` e `VACUUM`.

### 2.3 `OpenSintegra.Reports` (Exportação Documental)

- **`PdfReportGenerator.cs`:** Renderizador vetorial direto em `SkiaSharp.SKDocument.CreatePdf` em páginas A4 (`595 x 842 pt`) com paginação automática, cabeçalho institucional com monograma vetorial, blocos KPI em linguagem clara, tabela mensal com status XML, detalhamento de notas faltantes, ranking de produtos mais vendidos e rodapé numerado.
- **`ExcelReportGenerator.cs`:** Construtor de planilha multi-aba via `ClosedXML` gerando 8 abas (`Resumo Executivo`, `Movimentação Mensal`, `Produtos Mais Vendidos`, `Apuração por CFOP`, `Notas Reg 50`, `Saídas NFC-e Reg 61`, `Confronto XML`, `Diagnóstico`), utilizando fórmulas dinâmicas (`SUM`) e formatos monetários brasileiros (`R$ #,##0.00`).

### 2.4 `OpenSintegra.App` (Desktop UI Multiplataforma)

Desenvolvida em **Avalonia UI 11** usando Fluent Theme em paleta clara tradicional contábil, com:
- Barra de menus clássica + Ribbon com ícones vetoriais `StreamGeometry` (`VectorIcons.cs`).
- Barra lateral orientada ao fluxo real do contador + área de trabalho filtrada por competência via `ComboBox`.
- Listagens virtualizadas em `ListBox` separando **Entradas NF-e**, **Saídas NF-e** (Registro 50) e **Cupons NFC-e** (Registro 61), além de janelas dedicadas para detalhamento de itens de nota (`InvoiceDetailsWindow`), produtos mensais vendidos por cupom (`CouponMonthDetailsWindow`), detalhes de XMLs faltantes/auditados (`XmlInvoiceDetailsWindow`) e lançamento direto de XMLs faltantes (`InjectMissingXmlWindow`).
- Controles gráficos próprios em `DrawingContext` (`MonthlyBarChartControl` com hover e destaque de mês selecionado; `CompositionDonutControl`).
- Diálogo modal assíncrono não bloqueante (`ImportProgressWindow`) para arquivos extensos e ZIPs grandes.
- Janela de pré-visualização em papel A4 com zoom, troca de modo de relatório e exportação (`ReportPreviewWindow`).
- Inspetor binário/colunar (`LineInspectorWindow`) para diagnóstico preciso de linhas de 126 colunas.
