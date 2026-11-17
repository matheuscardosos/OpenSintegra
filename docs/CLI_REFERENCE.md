# Referencia Completa da Interface de Linha de Comando (`opensintegra-cli`)

O utilitario `opensintegra-cli` oferece todas as capacidades do motor fiscal em modo console e JSON para automacao de escritorios contabeis e pipelines CI/CD.

## Comandos Disponiveis

### `validate`
Valida um arquivo Sintegra mensal ou anual e calcula entradas, saidas e equilibrio fiscal.

```bash
opensintegra-cli validate <arquivo.txt> [--month YYYYMM] [--json] [--save-db] [--db caminho.db]
```
- Codigo de retorno: `0` se valido (0 erros), `1` se houver erros.

### `fix`
Aplica todas as correcoes automaticas e grava um novo arquivo padronizado.

```bash
opensintegra-cli fix <entrada.txt> [-o saida_corrigida.txt]
```

### `split`
Fatia um Sintegra anual consolidado em arquivos mensais individuais validos (`SINTEGRA_YYYYMM.TXT`).

```bash
opensintegra-cli split <anual.txt> [-d pasta_destino]
```

### `inspect`
Desmembra todas as colunas `1..126` de uma linha especifica para auditoria tecnica.

```bash
opensintegra-cli inspect <arquivo.txt> --line <numero_linha>
```

### `audit`
Executa auditoria cruzada do Sintegra contra pacotes `.zip`/pastas de XML e/ou SPED Fiscal.

```bash
opensintegra-cli audit <sintegra.txt> [--xml pacote.zip] [--sped sped.txt] [--month YYYYMM] [--json]
```

### `export-pdf` e `export-xlsx`
Gera relatorio vetorial em PDF ou planilha Excel multi-aba, opcionalmente filtrado para um unico mes e enriquecido com os resultados do confronto XML/SPED.

```bash
opensintegra-cli export-pdf <sintegra.txt> [-o relatorio.pdf] [--xml pacote.zip] [--month YYYYMM] [--regime "Simples Nacional"]
opensintegra-cli export-xlsx <sintegra.txt> [-o planilha.xlsx] [--xml pacote.zip] [--month YYYYMM] [--regime "Lucro Presumido"]
```

### `db-stats` e `db-history`
Consulta o tamanho usado, ajusta o limite em MB e lista empresas/competencias salvas na base SQLite local.

```bash
opensintegra-cli db-stats [--limit-mb 2048] [--db caminho.db]
opensintegra-cli db-history [--cnpj 11222333000181] [--db caminho.db]
```
