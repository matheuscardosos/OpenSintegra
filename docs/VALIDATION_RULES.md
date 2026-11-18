# Catálogo de Regras de Validação Fiscal

O motor `SintegraValidator` classifica cada achado em três níveis de severidade:
- **ERRO (`Severity.Error`):** Quebra especificada pelo Convênio ICMS 57/95 que impede aceitação em validadores estaduais padrão.
- **AVISO (`Severity.Warning`):** Divergência recomendável de revisão contábil (ex.: cálculo de ICMS ou validade do Registro 75).
- **INFO (`Severity.Info`):** Observação informativa (ex.: arquivo cobrindo período anual multi-mês para fatiamento posterior ou salto de numeração própria).

## Códigos de Diagnóstico

| Código | Severidade | Registro | Descrição | AutoFix |
| ------ | ---------- | -------- | --------- | ------- |
| `SIN-SYN-001` | ERRO | `--` | Arquivo Sintegra vazio (0 linhas). | Não |
| `SIN-SYN-002` | ERRO | Todos | Ausência de terminador de linha `CRLF` (`0x0D 0x0A`). | Sim |
| `SIN-SYN-003` | ERRO | Todos | Comprimento diferente de `126` colunas (respeitando exceções `88SMS`/`88SME`). | Sim (se padding seguro) |
| `SIN-SYN-004` | ERRO | `10`/`11` | Primeira linha diferente do `Reg 10` ou segunda linha diferente do `Reg 11`. | Sim |
| `SIN-SYN-005` | ERRO | `90` | Última linha do arquivo diferente do `Reg 90`. | Sim |
| `SIN-R10-001` | ERRO | `10` | CNPJ do estabelecimento inválido. | Não |
| `SIN-R10-002` | ERRO | `10` | Unidade da Federação (`UF`) ou Inscrição Estadual inválida para a UF. | Não |
| `SIN-R10-003` | ERRO | `10` | Data inicial ou final de competência inválida no `Reg 10`. | Não |
| `SIN-R10-004` | ERRO | `10` | Código de estrutura diferente de `3` para arquivos a partir de `01/01/2004`. | Não |
| `SIN-R10-005` | ERRO | `10` | Código de natureza (pos 125) ou código de finalidade (pos 126) inválido. | Não |
| `SIN-R10-006` | INFO | `10` | Período consolidado multi-mês detectado. Leitura anual e filtro mês a mês habilitados. | Via `SplitAnnualByMonth` |
| `SIN-R11-001` | AVISO | `11` | CEP inválido ou zerado no `Reg 11`. | Não |
| `SIN-R50-001` | ERRO | `50` | CNPJ/CPF, UF ou Inscrição Estadual inválida na NF. | Não |
| `SIN-R50-002` | ERRO | `50` | Data de emissão/recebimento inválida no `Reg 50`. | Não |
| `SIN-R50-003` | ERRO | `50` | Modelo de documento fiscal ou CFOP inválido no `Reg 50`. | Não |
| `SIN-R50-004` | ERRO | `50` | Nota fiscal cancelada (`Situação = S`) com valores contábeis/fiscais não zerados. | Sim |
| `SIN-R50-005` | AVISO | `50` | Divergência matemática entre `Base * Alíquota` e `Valor ICMS`. | Não |
| `SIN-R54-001` | ERRO | `54` | CFOP inválido ou código de produto em branco em item normal. | Não |
| `SIN-R61-001` | ERRO | `61` | Data de emissão inválida no `Reg 61`. | Não |
| `SIN-R61-002` | ERRO | `61` | Número final menor que o número inicial de ordem no `Reg 61`. | Não |
| `SIN-R61R-001` | ERRO | `61R` | Mês/ano (`MMAAAA`) inválido no `Reg 61R`. | Não |
| `SIN-R75-001` | ERRO | `75` | Código, descrição ou unidade em branco no `Reg 75`. | Não |
| `SIN-R75-002` | AVISO | `75` | Período de validade do `Reg 75` desalinhado em relação ao `Reg 10`. | Sim |
| `SIN-REL-001` | ERRO | `50` | Registro `50` duplicado para mesma NF, CFOP e alíquota. | Não |
| `SIN-REL-002` | ERRO | `54` | Item no `Reg 54` sem capa correspondente no `Reg 50`. | Não |
| `SIN-REL-003` | ERRO | `54` | Item acessório (`991`/`992`/`999`) isolado sem item normal na NF. | Não |
| `SIN-REL-004` | ERRO/AVISO | `50` | Divergência entre Valor Contábil do `Reg 50` e soma líquida dos itens do `Reg 54`. | Não |
| `SIN-REL-005` | ERRO | `54`/`60`/`61R`/`74` | Produto movimentado sem cadastro correspondente no `Reg 75`. | Sim |
| `SIN-REL-006` | ERRO | `75` | Registro `75` órfão (nenhum movimento no período faz referência ao código). | Sim |
| `SIN-REL-007` | ERRO | `75` | Código de produto duplicado na tabela `Reg 75`. | Sim |
| `SIN-REL-008` | ERRO | `60` | Registro `60A` sem `Reg 60M` correspondente na data/ECF. | Não |
| `SIN-REL-009` | ERRO | `90` | Totalizador no `Reg 90` ausente, incorreto ou divergente do CNPJ/IE do `Reg 10`. | Sim |
| `SIN-REL-010` | INFO | `50` | Salto de numeração em notas de emissão própria (`Emitente = P`). | Não |
| `SIN-REL-011` | AVISO | `90` | Bloco totalizador zerado (`00000000`) no `Reg 90` (regra 2192). | Sim |
