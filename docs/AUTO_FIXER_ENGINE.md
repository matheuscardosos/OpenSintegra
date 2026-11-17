# Motor de Correcao Automatica (Auto-Fixer)

O componente `SintegraAutoFixer` corrige divergencias estruturais e de totalizacao sem distorcer os dados contabeis das notas fiscais.

## Passos Executados por `SintegraAutoFixer.AutoFix(...)`

1. **`FIX-001` - Normalizacao de Terminadores `CRLF`:** Substitui finais `LF` ou `CR` soltos por `\r\n` padronizado.
2. **`FIX-002` - Normalizacao de Comprimento (`126` colunas):** Remove espacos excedentes alem da coluna 126 e preenche com espacos a direita **exclusivamente** os registros com sufixo livre (`10`, `11`, `53`, `55`, `56`, `60`, `61`, `74`, `75`, `77`, `85`, `86`, `88`). Registros com sufixo numerico ou de flag na coluna 126 (`50`, `51`, `54`, `70`, `76`) nunca recebem espacos cegos para nao corromper campos numericos.
3. **`FIX-003` - Remocao de Registros `75` Orfaos:** Remove produtos definidos no `Reg 75` que nao aparecem em nenhuma movimentacao do arquivo. Preserva obrigatoriamente codigos usados em `Reg 54`, `Reg 60D/I/R`, `Reg 61R`, `Reg 74`, `Reg 77` e `Reg 86`, evitando falsos orfaos.
4. **`FIX-004` - Sintese de Registros `75` Faltantes:** Cria definicao padrao valida no `Reg 75` quando algum item movimentado nao possui cadastro de produto correspondente.
5. **`FIX-005` - Sincronizacao de Datas do `Reg 75`:** Em arquivos mensais, alinha exatamente as datas inicial e final do `Reg 75` com o cabecalho `Reg 10`. Em arquivos anuais, garante intersecao valida com o periodo global.
6. **`FIX-006` - Deduplicacao de Registros `75`:** Elimina declaracoes repetidas para um mesmo codigo de produto.
7. **`FIX-007` - Zeramento de Notas Canceladas (`Reg 50 Situacao = S`):** Zera as colunas numericas `57..125` de documentos cancelados mantendo o identificador e marcador `S` na coluna 126.
8. **`FIX-008` - Ordenacao (`Reg 54` inclusa) e Reconstrucao do `Reg 90`:** Reordena os registros conforme sequencia canonica, ordena blocos contiguos do `Reg 54` pelo numero do item, omite `10`/`11` e totalizadores zerados, e gera tantos Registros `90` quantos necessarios (maximo 9 blocos por linha) encerrados pelo contador `99`.

## Fatiamento de Sintegra Anual (`SplitAnnualByMonth`)

Arquivos anuais consolidados permitem visao anual integrada no OpenSintegra, mas validadores estaduais tradicionais exigem entregas separadas por competencia mensal.

`SintegraAutoFixer.SplitAnnualByMonth(byte[] rawBytes)` extrai automaticamente cada competencia presente gerando arquivos `SINTEGRA_YYYYMM.TXT` completos com:
- `Reg 10` ajustado para `YYYYMM01..ultimo_dia_mes`
- `Reg 50`, `Reg 54` (vinculados ao mes da respectiva NF `Reg 50`), `Reg 61`, `Reg 61R` (`MMYYYY`), `Reg 70` do mes
- Tabela `Reg 75` enxuta com apenas os produtos utilizados naquela competencia e datas ajustadas ao mes
- Insercao automatica de `Reg 88SMS` caso nao haja saidas no mes
- `Reg 90` recalculado e validado com `0` erros
