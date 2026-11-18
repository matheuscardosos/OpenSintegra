# Histórico de Alterações (Changelog)

Todas as mudanças relevantes deste projeto são documentadas neste arquivo.
O formato segue o padrão [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/) e este projeto adere ao [Semantic Versioning 2.0.0](https://semver.org/lang/pt-BR/).

## [1.0.0] - 2026-11-17

### Adicionado
- Núcleo de análise Sintegra `OpenSintegra.Core` com suporte completo a arquivos mensais e anuais (Convênio ICMS 57/95: registros `10`, `11`, `50`, `51`, `53`, `54`, `60M/A/D/I/R`, `61`, `61R`, `70`, `74`, `75`, `77`, `85`, `86`, `88SMS/SME` e `90`).
- Filtro dinâmico mês a mês (`PeriodFilter.CreateScopedView`) aplicável a arquivos anuais e mensais sem quebrar a integridade dos totais, incluindo apuração de `Produtos Mais Vendidos` (`Reg 61R` e `Reg 54`).
- Motor de correção automática (`SintegraAutoFixer`) cobrindo `CRLF`, largura de 126 colunas, zeramento de canceladas, rastreamento completo de `Reg 75` referenciados por `54`/`60R`/`61R`/`74`/`77`/`86`, ordenação de itens `54` e reconstrução integral do `Reg 90`.
- Lançamento direto de XMLs de compra faltantes no Sintegra (`SintegraXmlInjector` e diálogo `InjectMissingXmlWindow`) gerando registros `50`, `54`, `75` e recalculando o Registro `90`.
- Função `SplitAnnualByMonth` para fatiamento de arquivos anuais em competências mensais independentes e válidas.
- Módulo de auditoria cruzada `FiscalCrossAuditor` com leitura assíncrona de pastas/ZIPs de XML (`NF-e`, `NFC-e`, `CT-e`, `NFS-e`, eventos de cancelamento) e SPED Fiscal `EFD ICMS/IPI` (`0200`, `C100`), tratamento de devoluções referenciadas (`refNFe`), comodatos (`5908`), bonificações (`5910`) e números módulo `1.000.000`.
- Módulo de persistência SQLite `OpenSintegra.Data` (`FiscalDatabase`) com histórico por empresa, linha do tempo mensal consolidada, preferências de último arquivo aberto, regime tributário e limite de tamanho configurável com auto-prune e `VACUUM`.
- Módulo de relatórios `OpenSintegra.Reports` com geração vetorial nativa em PDF (`SkiaSharp`) e pasta de trabalho Excel `.xlsx` de 8 abas com fórmulas (`ClosedXML`).
- Aplicação desktop multiplataforma `OpenSintegra.App` (Windows, Linux e macOS via Avalonia 11) com interface contábil tradicional, tabelas virtualizadas, detalhe completo de notas fiscais (`InvoiceDetailsWindow`), gráficos nativos, modal de progresso assíncrono, pré-visualização A4 e inspetor de colunas 1..126.
- CLI completa (`OpenSintegra.Cli`) para validação, correção, divisão mensal, inspeção de linhas, auditoria, exportação PDF/XLSX e manutenção do SQLite.
- Suíte de testes automatizados xUnit, scripts de empacotamento (`.exe` Inno Setup, `.deb` Debian, `.dmg` macOS) e workflows CI/CD GitHub Actions para Windows, Linux e macOS.
