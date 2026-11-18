# Referência Completa da Interface de Linha de Comando (`opensintegra-cli`)

O utilitário `opensintegra-cli` oferece todas as capacidades do motor fiscal em modo console e JSON para automação de escritórios contábeis e pipelines CI/CD.

## Comandos Disponíveis

### `validate`
Valida um arquivo Sintegra mensal ou anual e calcula entradas, saídas e saldo (`Saídas - Entradas`).

```bash
opensintegra-cli validate <arquivo.txt> [--month YYYYMM] [--json] [--save-db] [--db caminho.db]
```
- Código de retorno: `0` se válido (0 erros), `1` se houver erros.

### `fix`
Aplica todas as correções automáticas e grava um novo arquivo padronizado.

```bash
opensintegra-cli fix <entrada.txt> [-o saída_corrigida.txt]
```

### `split`
Fatia um Sintegra anual consolidado em arquivos mensais individuais válidos (`SINTEGRA_YYYYMM.TXT`).

```bash
opensintegra-cli split <anual.txt> [-d pasta_destino]
```

### `inspect`
Desmembra todas as colunas `1..126` de uma linha específica para auditoria técnica.

```bash
opensintegra-cli inspect <arquivo.txt> --line <número_linha>
```

### `audit`
Executa auditoria cruzada do Sintegra contra pacotes `.zip`/pastas de XML e/ou SPED Fiscal.

```bash
opensintegra-cli audit <sintegra.txt> [--xml pacote_ou_pasta] [--sped sped.txt] [--month YYYYMM] [--json]
```

### `reprocess-xml`
Reprocessa XMLs (de `.zip` ou pasta inteira recursivamente) atualizando a Data de Entrada e/ou CFOP no Registro `50`, incluindo notas faltantes, salvando automaticamente como `_ATUALIZADO.txt` (ou caminho `-o`) e opcionalmente gerando cópias ajustadas dos XMLs em pasta separada mantendo os originais intactos.

```bash
opensintegra-cli reprocess-xml <sintegra.txt> --xml <pacote_ou_pasta> [--entry-date DD/MM/AAAA] [--cfop 1102] [--only-missing] [--copy-xml-dir pasta_copias] [-o saída.txt]
```

### `export-pdf` e `export-xlsx`
Gera relatório vetorial em PDF ou planilha Excel multi-aba, opcionalmente filtrado para um único mês e enriquecido com os resultados do confronto XML/SPED.

```bash
opensintegra-cli export-pdf <sintegra.txt> [-o relatório.pdf] [--xml pacote.zip] [--month YYYYMM] [--regime "Simples Nacional"]
opensintegra-cli export-xlsx <sintegra.txt> [-o planilha.xlsx] [--xml pacote.zip] [--month YYYYMM] [--regime "Lucro Presumido"]
```

### `db-stats` e `db-history`
Consulta o tamanho usado, ajusta o limite em MB e lista empresas/competências salvas na base SQLite local.

```bash
opensintegra-cli db-stats [--limit-mb 2048] [--db caminho.db]
opensintegra-cli db-history [--cnpj 11222333000181] [--db caminho.db]
```
