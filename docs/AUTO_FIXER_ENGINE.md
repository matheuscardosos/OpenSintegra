# Motor de Correção Automática (Auto-Fixer)

O componente `SintegraAutoFixer` corrige divergências estruturais e de totalização sem distorcer os dados contábeis das notas fiscais.

## Passos Executados por `SintegraAutoFixer.AutoFix(...)`

1. **`FIX-001` - Normalização de Terminadores `CRLF`:** Substitui finais `LF` ou `CR` soltos por `\r\n` padronizado.
2. **`FIX-002` - Normalização de Comprimento (`126` colunas):** Remove espaços excedentes além da coluna 126 e preenche com espaços à direita **exclusivamente** os registros com sufixo livre (`10`, `11`, `53`, `55`, `56`, `60`, `61`, `74`, `75`, `77`, `85`, `86`, `88`). Registros com sufixo numérico ou de flag na coluna 126 (`50`, `51`, `54`, `70`, `76`) nunca recebem espaços cegos para não corromper campos numéricos.
3. **`FIX-003` - Remoção de Registros `75` Órfãos:** Remove produtos definidos no `Reg 75` que não aparecem em nenhuma movimentação do arquivo. Preserva obrigatoriamente códigos usados em `Reg 54`, `Reg 60D/I/R`, `Reg 61R`, `Reg 74`, `Reg 77` e `Reg 86`, evitando falsos órfãos.
4. **`FIX-004` - Síntese de Registros `75` Faltantes:** Cria definição padrão válida no `Reg 75` quando algum item movimentado não possui cadastro de produto correspondente.
5. **`FIX-005` - Sincronização de Datas do `Reg 75`:** Em arquivos mensais, alinha exatamente as datas inicial e final do `Reg 75` com o cabeçalho `Reg 10`. Em arquivos anuais, garante interseção válida com o período global.
6. **`FIX-006` - Deduplicação de Registros `75`:** Elimina declarações repetidas para um mesmo código de produto.
7. **`FIX-007` - Zeramento de Notas Canceladas (`Reg 50 Situação = S`):** Zera as colunas numéricas `57..125` de documentos cancelados mantendo o identificador e marcador `S` na coluna 126.
8. **`FIX-008` - Ordenação (`Reg 54` inclusa) e Reconstrução do `Reg 90`:** Reordena os registros conforme sequência canônica, ordena blocos contíguos do `Reg 54` pelo número do item, omite `10`/`11` e totalizadores zerados, e gera tantos Registros `90` quantos necessários (máximo 9 blocos por linha) encerrados pelo contador `99`.

## Fatiamento de Sintegra Anual (`SplitAnnualByMonth`)

Arquivos anuais consolidados permitem visão anual integrada no OpenSintegra, mas validadores estaduais tradicionais exigem entregas separadas por competência mensal.

`SintegraAutoFixer.SplitAnnualByMonth(byte[] rawBytes)` extrai automaticamente cada competência presente gerando arquivos `SINTEGRA_YYYYMM.TXT` completos com:
- `Reg 10` ajustado para `YYYYMM01..último_dia_mês`
- `Reg 50`, `Reg 54` (vinculados ao mês da respectiva NF `Reg 50`), `Reg 61`, `Reg 61R` (`MMYYYY`), `Reg 70` do mês
- Tabela `Reg 75` enxuta com apenas os produtos utilizados naquela competência e datas ajustadas ao mês
- Inserção automática de `Reg 88SMS` caso não haja saídas no mês
- `Reg 90` recalculado e validado com `0` erros
