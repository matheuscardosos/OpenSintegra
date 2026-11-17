# Catalogo de Regras de Validacao Fiscal

O motor `SintegraValidator` classifica cada achado em tres niveis de severidade:
- **ERRO (`Severity.Error`):** Quebra especificada pelo Convenio ICMS 57/95 que impede aceitacao em validadores estaduais padrao.
- **AVISO (`Severity.Warning`):** Divergencia recomendavel de revisao contabil (ex.: divergencia de inscricao estadual ou lacuna de sequencia).
- **INFO (`Severity.Info`):** Observacao informativa (ex.: arquivo cobrindo periodo anual multi-mes para fatiamento posterior).

## Codigos de Diagnostico

| Codigo | Severidade | Registro | Descricao | AutoFix |
| ------ | ---------- | -------- | --------- | ------- |
| `SIN-SYN-001` | ERRO | Arquivo | Arquivo Sintegra vazio (0 linhas). | Nao |
| `SIN-SYN-002` | ERRO | Todos | Ausencia de terminador de linha `CRLF` (`0x0D 0x0A`). | Sim |
| `SIN-SYN-003` | ERRO | Todos | Comprimento diferente de `126` colunas (respeitando excecoes `88SMS` MS). | Sim (se padding seguro) |
| `SIN-STR-001` | ERRO | `10`/`11` | Cabecalho `Reg 10` ou complementar `Reg 11` ausente ou fora da posicao 1/2. | Nao |
| `SIN-STR-002` | ERRO | `90` | Registro final `90` ausente ou nao localizado no encerramento do arquivo. | Sim |
| `SIN-CAD-001` | ERRO | `10`/`50`/`70` | CNPJ/CPF com formato ou digito verificador invalido. | Nao |
| `SIN-CAD-002` | ERRO | `10`..`70` | UF inexistente ou invalida. | Nao |
| `SIN-CAD-003` | ERRO/AVISO | `10`..`70` | Data fora do padrao `AAAAMMDD` ou fora do intervalo declarado no `Reg 10`. | Nao |
| `SIN-CAD-004` | AVISO | `10`/`50`/`70` | Inscricao Estadual invalida para a respectiva UF. | Nao |
| `SIN-PER-001` | INFO | `10` | Periodo consolidado multi-mes detectado (arquivo anual ou multi-competencia). Analise gerencial habilitada com filtro mes a mes. | Via `SplitAnnualByMonth` |
| `SIN-FIS-001` | ERRO | `50` | Nota fiscal cancelada (`Situacao = S`) apresentando valores diferentes de zero nos totalizadores. | Sim |
| `SIN-FIS-002` | ERRO | `50`/`54` | CFOP invalido (nao pertence aos grupos `1`, `2`, `3`, `5`, `6`, `7`). | Nao |
| `SIN-ORD-001` | ERRO | `54` | Itens do Registro `54` de uma mesma Nota Fiscal fora de ordem crescente. | Sim |
| `SIN-PRD-001` | ERRO | `75` | Produto referenciado por `54`, `60D/I/R`, `61R`, `74`, `77` ou `86` sem definicao correspondente no Registro `75`. | Sim |
| `SIN-PRD-002` | ERRO | `75` | Registro `75` orfao (nenhum movimento no periodo faz referencia ao codigo). | Sim |
| `SIN-PRD-003` | ERRO | `75` | Codigo de produto duplicado dentro da tabela `Reg 75`. | Sim |
| `SIN-PRD-004` | ERRO | `75` | Periodo de validade do Registro `75` desalinhado em relacao ao `Reg 10`. | Sim |
| `SIN-TOT-001` | ERRO | `90` | Totalizador no Registro `90` diverge da contagem fisica de linhas do arquivo. | Sim |
| `SIN-TOT-002` | AVISO | `90` | Inclusao indevida de totalizadores para os registros `10` ou `11` no `Reg 90`. | Sim |
| `SIN-TOT-003` | ERRO | `90` | Bloco de contagem zerado (`00000000`) presente no Registro `90`. | Sim |
