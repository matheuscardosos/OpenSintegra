# Historico de Alteracoes (Changelog)

Todas as mudancas relevantes deste projeto sao documentadas neste arquivo.
O formato segue o padrao [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/) e este projeto adere ao [Semantic Versioning 2.0.0](https://semver.org/lang/pt-BR/).

## [1.0.0] - 2026-11-17

### Adicionado
- Nucleo de analise Sintegra `OpenSintegra.Core` com suporte completo a arquivos mensais e anuais (Convenio ICMS 57/95: registros `10`, `11`, `50`, `51`, `53`, `54`, `60M/A/D/I/R`, `61`, `61R`, `70`, `74`, `75`, `77`, `85`, `86`, `88SMS/SME` e `90`).
- Filtro dinamico mes a mes (`PeriodFilter.CreateScopedView`) aplicavel a arquivos anuais e mensais sem quebrar a integridade dos totais.
- Motor de correcao automatica (`SintegraAutoFixer`) cobrindo `CRLF`, largura de 126 colunas, zeramento de canceladas, rastreamento completo de `Reg 75` referenciados por `54`/`60R`/`61R`/`74`/`77`/`86`, ordenacao de itens `54` e reconstrucao integral do `Reg 90`.
- Funcao `SplitAnnualByMonth` para fatiamento de arquivos anuais em competencias mensais independentes e validas.
- Modulo de auditoria cruzada `FiscalCrossAuditor` com leitura assincrona de pastas/ZIPs de XML (`NF-e`, `NFC-e`, `CT-e`, `NFS-e`, eventos de cancelamento) e SPED Fiscal `EFD ICMS/IPI` (`0200`, `C100`), tratamento de devolucoes referenciadas (`refNFe`), comodatos (`5908`), bonificacoes (`5910`) e numeros modulo `1.000.000`.
- Modulo de persistencia SQLite `OpenSintegra.Data` (`FiscalDatabase`) com historico por empresa, linha do tempo mensal consolidada, regime tributario e limite de tamanho configuravel com auto-prune e `VACUUM`.
- Modulo de relatorios `OpenSintegra.Reports` com geracao vetorial nativa em PDF (`SkiaSharp`) e pasta de trabalho Excel `.xlsx` multi-aba com formulas (`ClosedXML`).
- Aplicacao desktop multiplataforma `OpenSintegra.App` (Windows, Linux e macOS via Avalonia 11) com interface contabil tradicional, graficos nativos de barras e composicao, modal de progresso assincrono, pre-visualizacao A4 e inspetor de colunas 1..126.
- CLI completa (`OpenSintegra.Cli`) para validacao, correcao, divisao mensal, inspecao de linhas, auditoria, exportacao PDF/XLSX e manutencao do SQLite.
- Suite de testes automatizados xUnit e workflows CI/CD GitHub Actions para Windows, Linux e macOS.
