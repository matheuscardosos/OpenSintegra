# Especificação Técnica - Convênio ICMS 57/95 (Sintegra)

Este documento resume a estrutura de registros suportada pelo **OpenSintegra Contábil**, conforme os layouts oficiais do Convênio ICMS 57/95 e extensões estaduais.

## Regras Gerais de Formatação

- **Codificação:** Texto plano `ASCII` / `ISO-8859-1 (Latin1)`.
- **Comprimento da Linha:** Exatamente `126` caracteres visíveis por registro (colunas `1..126`), exceto registros estaduais específicos (como `88SMS`/`88SME` no MS que variam entre `126` e `140` colunas).
- **Terminador de Linha:** Padrão DOS/Windows `CRLF` (`\r\n`, bytes `0x0D 0x0A`).
- **Tipos de Campos:**
  - **Numérico (`N`):** Alinhado à direita, preenchido com zeros (`0`) à esquerda. Valores monetários armazenados em centavos (`2` casas decimais implícitas, exceto quantidades em `Reg 54` e `61R` com `3` decimais).
  - **Alfanumérico (`X`):** Alinhado à esquerda, preenchido com espaços (` `) à direita.

## Tabela de Registros Suportados

| Tipo | Subtipo | Descrição | Campos-Chave |
| ---- | ------- | --------- | ------------ |
| `10` | - | Mestre do Estabelecimento | CNPJ, IE, Razão Social, Município, UF, Data Inicial (`108..115`), Data Final (`116..123`), Convênio (`124`), Natureza (`125`), Finalidade (`126`) |
| `11` | - | Dados Complementares | Logradouro, Número, Complemento, Bairro, CEP, Contato, Telefone |
| `50` | - | Nota Fiscal (Entrada/Saída, ICMS) | CNPJ (`3..16`), IE, Data, UF, Modelo, Série, Número (`46..51`), CFOP (`52..55`), Emitente (`P`/`T`), Valores, Situação (`N`/`S`/`E`/`X`) |
| `51` | - | Total de Nota Fiscal quanto ao IPI | CNPJ, Data, Série, Número, CFOP, Valor Total, IPI, Isentas/Outras |
| `53` | - | Substituição Tributária | CNPJ, Data, Modelo, Série, Número, CFOP, Base ST, ICMS Retido, Despesas |
| `54` | - | Item de Nota Fiscal (Mercadoria/Serviço) | CNPJ, Modelo, Série, Número, CFOP, CST, NumItem (`001..990` / `991..999`), Código Produto (`38..51`), Quantidade, Valor, IPI |
| `60` | `M/A/D/I/R` | Cupom Fiscal / PDV / ECF | Resumo diário, alíquotas, itens e resumo mensal de produtos |
| `61` | - | Documentos Fiscais não emitidos por ECF / NFC-e | Data (`31..38`), Modelo (`02`/`65`), Série, Num. Inicial/Final de Ordem, Valor Total (`58..70`), Base ICMS, ICMS, Alíquota |
| `61` | `R` | Resumo Mensal de Mercadorias (NFC-e / Mod 02) | MMAAAA (`4..9`), Código Produto (`10..23`), Quantidade, Valor Bruto, Base ICMS, Alíquota |
| `70` | - | Conhecimento de Transporte (CT-e / CTRC) | CNPJ, IE, Data, UF, Modelo, Série, Número, CFOP, Valores, Modalidade Frete (`CIF`/`FOB`), Situação |
| `74` | - | Registro de Inventário | Data Inventário, Código Produto (`11..24`), Quantidade, Valor Produto, Código Posse, CNPJ Possuidor |
| `75` | - | Tabela de Código de Produtos e Serviços | Data Inicial (`3..10`), Data Final (`11..18`), Código Produto (`19..32`), NCM (`33..40`), Descrição (`41..93`), Unidade (`94..99`), IPI, ICMS, Redução, Base ST |
| `77` | - | Serviços de Comunicação e Telecomunicação | Identificação do tomador/prestador, Código Produto e valores |
| `85/86` | - | Informações de Exportação | Declaração de exportação, registro de exportação e produtos |
| `88` | `SMS/SME` | Registros Estaduais Sem Movimento (MS/outros) | Indicador de ausência de movimentação de saída ou entrada no período |
| `90` | - | Totalização do Arquivo | CNPJ, IE, blocos repetíveis `Tipo(2) + Contagem(8)` omitindo `10`/`11` e totalizando `99` (total físico de linhas) |

## Particularidade de Numeração NF-e (> 6 dígitos)

Como os registros `50`, `51`, `53`, `54` e `70` reservam apenas **6 posições** para o campo Número da Nota Fiscal (colunas `46..51`), documentos eletrônicos modelo `55` cuja numeração ultrapassa `999.999` utilizam os **6 últimos dígitos** (`número % 1.000.000`). O motor de cruzamento XML/SPED e o injetor `SintegraXmlInjector` contemplam essa regra automaticamente.
