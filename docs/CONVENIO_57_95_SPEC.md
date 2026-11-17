# Especificacao Tecnica - Convenio ICMS 57/95 (Sintegra)

Este documento resume a estrutura de registros suportada pelo **OpenSintegra Contabil**, conforme os layouts oficiais do Convenio ICMS 57/95 e extensoes estaduais.

## Regras Gerais de Formatacao

- **Codificacao:** Texto plano `ASCII` / `ISO-8859-1 (Latin1)`.
- **Comprimento da Linha:** Exatamente `126` caracteres visiveis por registro (colunas `1..126`), exceto registros estaduais especificos (como `88SMS`/`88SME` no MS que variam entre `126` e `140` colunas).
- **Terminador de Linha:** Padrao DOS/Windows `CRLF` (`\r\n`, bytes `0x0D 0x0A`).
- **Tipos de Campos:**
  - **Numerico (`N`):** Alinhado a direita, preenchido com zeros (`0`) a esquerda. Valores monetarios armazenados em centavos (`2` casas decimais implicitas, exceto quantidades em `Reg 54` e `61R` com `3` decimais).
  - **Alfanumerico (`X`):** Alinhado a esquerda, preenchido com espacos (` `) a direita.

## Tabela de Registros Suportados

| Tipo | Subtipo | Descricao | Campos-Chave |
| ---- | ------- | --------- | ------------ |
| `10` | - | Mestre do Estabelecimento | CNPJ, IE, Razao Social, Municipio, UF, Data Inicial (`108..115`), Data Final (`116..123`), Convenio (`124`), Natureza (`125`), Finalidade (`126`) |
| `11` | - | Dados Complementares | Logradouro, Numero, Complemento, Bairro, CEP, Contato, Telefone |
| `50` | - | Nota Fiscal (Entrada/Saida, ICMS) | CNPJ (`3..16`), IE, Data, UF, Modelo, Serie, Numero (`46..51`), CFOP (`52..55`), Emitente (`P`/`T`), Valores, Situacao (`N`/`S`/`E`/`X`) |
| `51` | - | Total de Nota Fiscal quanto ao IPI | CNPJ, Data, Serie, Numero, CFOP, Valor Total, IPI, Isentas/Outras |
| `53` | - | Substituicao Tributaria | CNPJ, Data, Modelo, Serie, Numero, CFOP, Base ST, ICMS Retido, Despesas |
| `54` | - | Item de Nota Fiscal (Mercadoria/Servico) | CNPJ, Modelo, Serie, Numero, CFOP, CST, NumItem (`001..990` / `991..999`), Codigo Produto (`38..51`), Quantidade, Valor, IPI |
| `60` | `M/A/D/I/R` | Cupom Fiscal / PDV / ECF | Resumo diario, aliquotas, itens e resumo mensal de produtos |
| `61` | - | Documentos Fiscais nao emitidos por ECF / NFC-e | Data (`31..38`), Modelo (`02`/`65`), Serie, Num. Inicial/Final de Ordem, Valor Total (`58..70`), Base ICMS, ICMS, Aliquota |
| `61` | `R` | Resumo Mensal de Mercadorias (NFC-e / Mod 02) | MMAAAA (`4..9`), Codigo Produto (`10..23`), Quantidade, Valor Bruto, Base ICMS, Aliquota |
| `70` | - | Conhecimento de Transporte (CT-e / CTRC) | CNPJ, IE, Data, UF, Modelo, Serie, Numero, CFOP, Valores, Modalidade Frete (`CIF`/`FOB`), Situacao |
| `74` | - | Registro de Inventario | Data Inventario, Codigo Produto (`11..24`), Quantidade, Valor Produto, Codigo Posse, CNPJ Possuidor |
| `75` | - | Tabela de Codigo de Produtos e Servicos | Data Inicial (`3..10`), Data Final (`11..18`), Codigo Produto (`19..32`), NCM (`33..40`), Descricao (`41..93`), Unidade (`94..99`), IPI, ICMS, Reducao, Base ST |
| `77` | - | Servicos de Comunicacao e Telecomunicacao | Identificacao do tomador/prestador, Codigo Produto e valores |
| `85/86` | - | Informacoes de Exportacao | Declaracao de exportacao, registro de exportacao e produtos |
| `88` | `SMS/SME` | Registros Estaduais Sem Movimento (MS/outros) | Indicador de ausencia de movimentacao de saida ou entrada no periodo |
| `90` | - | Totalizacao do Arquivo | CNPJ, IE, blocos repetiveis `Tipo(2) + Contagem(8)` omitindo `10`/`11` e totalizando `99` (total fisico de linhas) |

## Particularidade de Numeracao NF-e (> 6 digitos)

Como os registros `50`, `51`, `53`, `54` e `70` reservam apenas **6 posicoes** para o campo Numero da Nota Fiscal (colunas `46..51`), documentos eletronicos modelo `55` cuja numeracao ultrapassa `999.999` utilizam os **6 ultimos digitos** (`numero % 1.000.000`). O motor de cruzamento XML/SPED do OpenSintegra contempla essa regra automaticamente.
