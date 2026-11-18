# Guia de Auditoria Cruzada (XML e SPED Fiscal)

O módulo `FiscalCrossAuditor` compara o arquivo Sintegra contra documentos fiscais eletrônicos originais (`.xml`, diretórios recursivos ou pacotes `.zip`) e contra arquivos do SPED Fiscal `EFD ICMS/IPI`.

## Como o Cruzamento Classifica os Documentos

Ao analisar XMLs contra o Registro `50` e `70` do Sintegra, cada nota recebe uma categoria operacional para evitar falsos positivos na apuração de compras omitidas:

1. **Compra Comercial (`CompraComercial`):** Notas de terceiros destinadas ao contribuinte com finalidade normal e CFOPs de operação comercial. São os documentos críticos monitorados para identificar compras não lançadas (`XmlComprasOmissas`).
2. **Devolução / Retorno Referenciado (`tpNF=0` + `refNFe`):** Quando há uma nota de devolução ou recusa referenciando a chave de acesso de uma compra anterior, o algoritmo identifica o estorno da operação e não aponta a nota original devolvida como compra faltante crítica.
3. **Bonificação / Brinde (`CFOP 5910 / 6910`):** Classificada separadamente para revisão gerencial sem contaminar o total de compras comerciais.
4. **Comodato (`CFOP 5908 / 6908`):** Identificada e segregada automaticamente.
5. **NFC-e Consumidor (`Modelo 65`) e Saídas Próprias:** Analisadas quanto ao volume consolidado frente ao Registro `61` ou `50`.
6. **Eventos de Cancelamento (`110111`):** Cancelamentos detectados via XML ajustam o status esperado no Sintegra (`Situação = S`).

## Correspondência Inteligente de Numeração

Como o Registro `50` limita o número do documento a `6` dígitos, o cruzamento realiza pareamento normalizando:
- Chave primária completa: `CNPJ + Modelo + SérieNormalizada + (Número % 1.000.000)`
- Chave secundária de fallback (quando o CNPJ emitente diverge pontualmente): `Modelo + SérieNormalizada + (Número % 1.000.000)`

## Cruzamento com SPED Fiscal (EFD ICMS/IPI)

Ao carregar um arquivo SPED (`.txt` separado por `|`), o auditor extrai os registros `0000`, `0200` e `C100`, sinalizando:
- Notas escrituradas no Bloco C (`C100`) ausentes no `Reg 50` do Sintegra (`AUD-SPD-001`)
- Divergências de Valor Contábil superiores a R$ 0,02 (`AUD-SPD-003`)
- Divergências de situação normal vs cancelada entre `COD_SIT` e `Situação` (`AUD-SPD-004`)
