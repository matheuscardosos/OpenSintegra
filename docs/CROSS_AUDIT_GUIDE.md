# Guia de Auditoria Cruzada (XML e SPED Fiscal)

O modulo `FiscalCrossAuditor` compara o arquivo Sintegra contra documentos fiscais eletronicos originais (`.xml`, diretorios recursivos ou pacotes `.zip`) e contra arquivos do SPED Fiscal `EFD ICMS/IPI`.

## Como o Cruzamento Classifica os Documentos

Ao analisar XMLs contra o Registro `50` e `70` do Sintegra, cada nota recebe uma categoria operacional para evitar falsos positivos na apuracao de compras omitidas:

1. **Compra Comercial (`CompraComercial`):** Notas de terceiros destinadas ao contribuinte com finalidade normal e CFOPs de operacao comercial. Sao os documentos criticos monitorados para identificar compras nao lancadas (`XmlComprasOmissas`).
2. **Devolucao / Retorno Referenciado (`tpNF=0` + `refNFe`):** Quando ha uma nota de devolucao ou recusa referenciando a chave de acesso de uma compra anterior, o algoritmo identifica o estorno da operacao e nao aponta a nota original devolvida como compra faltante critica.
3. **Bonificacao / Brinde (`CFOP 5910 / 6910`):** Classificada separadamente para revisao gerencial sem contaminar o total de compras comerciais.
4. **Comodato (`CFOP 5908 / 6908`):** Identificada e segregada automaticamente.
5. **NFC-e Consumidor (`Modelo 65`) e Saidas Proprias:** Analisadas quanto ao volume consolidado frente ao Registro `61` ou `50`.
6. **Eventos de Cancelamento (`110111`):** Cancelamentos detectados via XML ajustam o status esperado no Sintegra (`Situacao = S`).

## Correspondencia Inteligente de Numeracao

Como o Registro `50` limita o numero do documento a `6` digitos, o cruzamento realiza pareamento normalizando:
- Chave primeria completa: `CNPJ + Modelo + SerieNormalizada + (Numero % 1.000.000)`
- Chave secundaria de fallback (quando CNPJ emitente diverge pontualmente): `Modelo + SerieNormalizada + (Numero % 1.000.000)`

## Cruzamento com SPED Fiscal (EFD ICMS/IPI)

Ao carregar um arquivo SPED (`.txt` separado por `|`), o auditor extrai os registros `0000`, `0200` e `C100`, sinalizando:
- Notas escrituradas no Bloco C (`C100`) ausentes no `Reg 50` do Sintegra (`AUD-SPD-001`)
- Divergencias de Valor Contabil superiores a R$ 0,02 (`AUD-SPD-003`)
- Divergencias de situacao normal vs cancelada entre `COD_SIT` e `Situacao` (`AUD-SPD-004`)
