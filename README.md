<p align="center">
  <img src="assets/brand/open-sintegra-logo.png" alt="OpenSintegra Contábil" width="460" />
</p>

# OpenSintegra Contábil

[![CI Build and Test](https://github.com/opensintegra/open-sintegra/actions/workflows/ci.yml/badge.svg)](https://github.com/opensintegra/open-sintegra/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)](#instalação-e-execução)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)

Suíte contábil multiplataforma de código aberto (Licença MIT) para validação, correção automática, auditoria cruzada (XML NF-e/NFC-e/CT-e e SPED EFD ICMS/IPI), lançamento direto de notas fiscais omitidas e análise gerencial de arquivos **Sintegra (Convênio ICMS 57/95)**, com suporte nativo a arquivos **mensais e anuais**.

## Principais Capacidades

- **Validação Anual e Mensal Completa (Convênio ICMS 57/95):** Suporte integral aos registros `10`, `11`, `50`, `51`, `53`, `54`, `60M/A/D/I/R`, `61`, `61R`, `70`, `74`, `75`, `77`, `85`, `86`, `88SMS/SME` e `90`, inclusive em arquivos consolidados de múltiplos meses sem interrupção.
- **Filtro Gerencial Mês a Mês:** Selecione o período completo ou qualquer competência individual (`YYYYMM`) com atualização imediata de KPIs, gráficos comparativos, notas fiscais, produtos mais vendidos e relatórios.
- **Motor de Correção Automática (`Auto-Fixer`):** Corrige terminadores `CRLF`, largura fixa de 126 colunas quando seguro, zeramento de notas canceladas (`Situação = S`), remoção de Registros `75` órfãos (preservando códigos referenciados em `61R` e `60R`), síntese de `75` faltantes, sincronização de datas, ordenação de itens `54` e reconstrução integral do Registro `90` (omitindo tipos `10`, `11` e contadores zerados).
- **Lançamento Direto de XMLs Faltantes no Sintegra (`SintegraXmlInjector`):** Permite selecionar notas de compra presentes nos XMLs que ficaram fora do Sintegra, conferir/editar o CFOP de entrada e gerar automaticamente os Registros `50`, `54` (itens) e `75` (produtos) com recálculo completo do Registro `90`.
- **Divisão Anual em Arquivos Mensais Validados:** Quebra um Sintegra anual em arquivos mensais independentes (`SINTEGRA_YYYYMM.TXT`), cada um com seu próprio cabeçalho `10/11`, `75` válido, `88SMS` quando aplicável e `90` exato.
- **Confronto XML / SPED Fiscal:** Compara o Sintegra contra pacotes `.zip`/pastas de XML (`NF-e`, `NFC-e`, `CT-e`, eventos de cancelamento) e SPED Fiscal `C100`. Identifica compras comerciais não lançadas por competência, reconcilia devoluções (`tpNF=0` com `refNFe`), comodatos (`CFOP 5908`) e bonificações (`CFOP 5910`), além de normalização módulo `1.000.000` para números com mais de 6 dígitos.
- **Persistência Local em SQLite (`OpenSintegra.Data`):** Armazena histórico de empresas, importações por hash SHA-256, preferências de último arquivo aberto, evolução mensal consolidada e controle configurável de espaço máximo em disco (`max_db_size_mb`, Auto-Prune e `VACUUM`).
- **Relatórios Vetoriais em PDF e Planilhas Excel (`.xlsx`):** Visualizador de impressão integrado com modos para o contador e para o cliente (Completo, Somente Notas Não Lançadas e Produtos Mais Vendidos), geração nativa de PDF (`SkiaSharp`) e pasta de trabalho Excel de 8 abas com fórmulas (`ClosedXML`).
- **Aplicação Desktop Tradicional + CLI:** Interface desktop nativa multiplataforma em Avalonia UI (estilo suíte contábil clássica) com listagem virtualizada de notas fiscais e inspetor detalhado de itens, acompanhada do utilitário de linha de comando (`opensintegra-cli`).

## Arquitetura dos Projetos

```text
OpenSintegra.sln
├── src/
│   ├── OpenSintegra.Core/      # Parser, TaxIdValidator, Validator, AutoFixer, XmlInjector, CrossAuditor, PeriodFilter
│   ├── OpenSintegra.Data/      # Repositório SQLite (empresas, histórico mensal, configurações, cotas)
│   ├── OpenSintegra.Reports/   # Geradores PDF vetorial (SkiaSharp) e Excel multi-aba (ClosedXML)
│   ├── OpenSintegra.App/       # Interface desktop multiplataforma (Avalonia UI)
│   └── OpenSintegra.Cli/       # Interface de linha de comando para validação, auditoria e exportação
├── tests/
│   └── OpenSintegra.Tests/     # Suíte de testes automatizados xUnit
└── docs/                       # Especificações técnicas do Convênio 57/95, regras, CLI e banco SQLite
```

Para detalhes arquiteturais aprofundados, consulte [ARCHITECTURE.md](ARCHITECTURE.md).

## Instalação e Execução

### Opções de Distribuição (Releases)

Cada versão publicada oferece **Instalador Tradicional** e **Pacote Portátil**:
- **Windows (`win-x64`):** `OpenSintegra-Setup-<versao>-win-x64.exe` (instalador completo com Menu Iniciar, atalho na Área de Trabalho e desinstalador) e pacote portátil `.zip`.
- **Linux (`linux-x64`):** pacote Debian `.deb` (`opensintegra_<versao>_amd64.deb`) e `.tar.gz` portátil.
- **macOS (`osx-arm64`):** imagem `.dmg` (`OpenSintegra-<versao>-osx-arm64.dmg`) e `.tar.gz` portátil.

### Requisitos (para compilar do código-fonte)

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

Ou passando arquivo Sintegra e pacote ZIP de XMLs diretamente por parâmetro:

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

## Documentação Técnica Completa

- [Guia de Arquitetura](ARCHITECTURE.md)
- [Especificação Convênio ICMS 57/95](docs/CONVENIO_57_95_SPEC.md)
- [Catálogo de Regras de Validação](docs/VALIDATION_RULES.md)
- [Motor de Correção Automática (`Auto-Fixer`)](docs/AUTO_FIXER_ENGINE.md)
- [Guia de Confronto XML e SPED Fiscal](docs/CROSS_AUDIT_GUIDE.md)
- [Persistência SQLite e Limite de Armazenamento](docs/DATABASE_STORAGE.md)
- [Geração de Relatórios PDF e Excel](docs/REPORTING_EXPORT.md)
- [Referência Completa do CLI](docs/CLI_REFERENCE.md)

## Contribuição e Governança

Contribuições são muito bem-vindas! Leia [CONTRIBUTING.md](CONTRIBUTING.md), [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [SECURITY.md](SECURITY.md) e as diretrizes para agentes de desenvolvimento em [AGENTS.md](AGENTS.md) antes de abrir um Pull Request.

## Licença

Este projeto é distribuído sob a [Licença MIT](LICENSE).
