<p align="center">
  <img src="assets/brand/open-sintegra-logo.png" alt="OpenSintegra Contabil" width="460" />
</p>

# OpenSintegra Contabil

[![CI Build and Test](https://github.com/opensintegra/open-sintegra/actions/workflows/ci.yml/badge.svg)](https://github.com/opensintegra/open-sintegra/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)](#instalacao-e-execucao)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)

Suite contabil multiplataforma de codigo aberto (Licenca MIT) para validacao, correcao automatica, auditoria cruzada (XML NF-e/NFC-e/CT-e e SPED EFD ICMS/IPI) e analise gerencial de arquivos **Sintegra (Convenio ICMS 57/95)**, com suporte nativo a arquivos **mensais e anuais**.

## Principais Capacidades

- **Validacao Anual e Mensal Completa (Convenio ICMS 57/95):** Suporte integral aos registros `10`, `11`, `50`, `51`, `53`, `54`, `60M/A/D/I/R`, `61`, `61R`, `70`, `74`, `75`, `77`, `85`, `86`, `88SMS/SME` e `90`, inclusive em arquivos consolidados de multiplos meses sem interrupcao.
- **Filtro Gerencial Mes a Mes:** Selecione o periodo completo ou qualquer competencia individual (`YYYYMM`) com atualizacao imediata de KPIs, graficos comparativos, notas fiscais e relatorios.
- **Motor de Correcao Automatica (`Auto-Fixer`):** Corrige terminadores `CRLF`, largura fixa de 126 colunas quando seguro, zeramento de notas canceladas (`Situacao = S`), remocao de Registros `75` orfaos (preservando codigos referenciados em `61R` e `60R`), sintese de `75` faltantes, sincronizacao de datas, ordenacao de itens `54` e reconstrucao integral do Registro `90` (omitindo tipos `10`, `11` e contadores zerados).
- **Divisao Anual em Arquivos Mensais Validados:** Quebra um Sintegra anual em arquivos mensais independentes (`SINTEGRA_YYYYMM.TXT`), cada um com seu proprio cabecalho `10/11`, `75` valido, `88SMS` quando aplicavel e `90` exato.
- **Confronto XML / SPED Fiscal:** Compara o Sintegra contra pacotes `.zip`/pastas de XML (`NF-e`, `NFC-e`, `CT-e`, eventos de cancelamento) e SPED Fiscal `C100`. Identifica compras comerciais nao lancadas, reconcilia devolucoes (`tpNF=0` com `refNFe`), comodatos (`CFOP 5908`) e bonificacoes (`CFOP 5910`), alem de normalizacao modulo `1.000.000` para numeros com mais de 6 digitos.
- **Persistencia Local em SQLite (`OpenSintegra.Data`):** Armazena historico de empresas, importacoes por hash SHA-256, evolucao mensal consolidada entre uploads separados e controle configuravel de espaco maximo em disco (`max_db_size_mb`, Auto-Prune e `VACUUM`).
- **Relatorios Vetoriais em PDF e Planilhas Excel (`.xlsx`):** Visualizador de impressao integrado com geracao nativa de PDF (SkiaSharp) e pasta de trabalho Excel multi-aba formatada com formulas (ClosedXML).
- **Aplicacao Desktop Tradicional + CLI:** Interface desktop nativa multiplataforma em Avalonia UI (estilo suite contabil classica) e utilitario de linha de comando (`opensintegra-cli`) pronto para automacao e pipelines CI.

## Arquitetura dos Projetos

```text
OpenSintegra.sln
├── src/
│   ├── OpenSintegra.Core/      # Parser, TaxIdValidator, Validator, AutoFixer, CrossAuditor, PeriodFilter
│   ├── OpenSintegra.Data/      # Repositorio SQLite (empresas, historico mensal, cotas configuraveis)
│   ├── OpenSintegra.Reports/   # Geradores PDF vetorial (SkiaSharp) e Excel multi-aba (ClosedXML)
│   ├── OpenSintegra.App/       # Interface desktop multiplataforma (Avalonia UI)
│   └── OpenSintegra.Cli/       # Interface de linha de comando para validacao, auditoria e exportacao
├── tests/
│   └── OpenSintegra.Tests/     # Suite de testes automatizados xUnit
└── docs/                       # Especificacoes tecnicas do Convenio 57/95, regras, CLI e banco SQLite
```

Para detalhes arquiteturais aprofundados, consulte [ARCHITECTURE.md](ARCHITECTURE.md).

## Instalacao e Execucao

### Opcoes de Distribuicao (Releases)

Cada versao publicada oferece **Instalador Tradicional** e **Pacote Portatil**:
- **Windows (`win-x64`):** `OpenSintegra-Setup-<versao>-win-x64.exe` (instalador completo com Menu Iniciar, atalho na Area de Trabalho e desinstalador) e pacote portatil `.zip`.
- **Linux (`linux-x64`):** pacote Debian `.deb` (`opensintegra_<versao>_amd64.deb`) e `.tar.gz` portatil.
- **macOS (`osx-arm64`):** imagem `.dmg` (`OpenSintegra-<versao>-osx-arm64.dmg`) e `.tar.gz` portatil.

### Requisitos (para compilar do codigo-fonte)

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows, Linux ou macOS)

### Compilar e Testar

```bash
git clone https://github.com/opensintegra/open-sintegra.git
cd open-sintegra
dotnet restore
dotnet test
```

### Executar a Interface Desktop

```bash
dotnet run --project src/OpenSintegra.App
```

Ou passando arquivo Sintegra e pacote ZIP de XMLs diretamente por parametro:

```bash
dotnet run --project src/OpenSintegra.App -- "C:\caminho\Sintegra_2026.txt" "C:\caminho\XMLs_2026.zip"
```

### Executar o CLI

```bash
dotnet run --project src/OpenSintegra.Cli -- validate "Sintegra_2026.txt" --save-db
dotnet run --project src/OpenSintegra.Cli -- audit "Sintegra_2026.txt" --xml "XMLs_2026.zip" --month 202605
dotnet run --project src/OpenSintegra.Cli -- export-pdf "Sintegra_2026.txt" --xml "XMLs_2026.zip" -o relatorio.pdf
dotnet run --project src/OpenSintegra.Cli -- export-xlsx "Sintegra_2026.txt" --xml "XMLs_2026.zip" -o planilha.xlsx
dotnet run --project src/OpenSintegra.Cli -- fix "Sintegra_2026.txt" -o "Sintegra_2026_CORRIGIDO.txt"
dotnet run --project src/OpenSintegra.Cli -- split "Sintegra_2026.txt" -d ./mensais
```

Mais exemplos em [docs/CLI_REFERENCE.md](docs/CLI_REFERENCE.md).

## Documentacao Tecnica Completa

- [Guia de Arquitetura](ARCHITECTURE.md)
- [Especificacao Convenio ICMS 57/95](docs/CONVENIO_57_95_SPEC.md)
- [Catalogo de Regras de Validacao](docs/VALIDATION_RULES.md)
- [Motor de Correcao Automatica (`Auto-Fixer`)](docs/AUTO_FIXER_ENGINE.md)
- [Guia de Confronto XML e SPED Fiscal](docs/CROSS_AUDIT_GUIDE.md)
- [Persistencia SQLite e Limite de Armazenamento](docs/DATABASE_STORAGE.md)
- [Geracao de Relatorios PDF e Excel](docs/REPORTING_EXPORT.md)
- [Referencia Completa do CLI](docs/CLI_REFERENCE.md)

## Contribuicao e Governanca

Contribuicoes sao muito bem-vindas! Leia [CONTRIBUTING.md](CONTRIBUTING.md), [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [SECURITY.md](SECURITY.md) e as diretrizes para agentes de desenvolvimento em [AGENTS.md](AGENTS.md) antes de abrir um Pull Request.

## Licenca

Este projeto e distribuido sob a [Licenca MIT](LICENSE).
