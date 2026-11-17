# Geracao de Relatorios Vetoriais (PDF) e Planilhas Excel (.xlsx)

O modulo `OpenSintegra.Reports` utiliza exclusivamente bibliotecas permissivas de codigo aberto (`SkiaSharp` e `ClosedXML`), funcionando de forma identica no Windows, Linux e macOS sem depender de Microsoft Office ou suites proprietarias instaladas.

Todos os relatorios respeitam integralmente o filtro ativo na tela ou no parametro `--month YYYYMM` da CLI atraves de `ScopedFiscalView`.

## 1. Relatorio Vetorial em PDF (`PdfReportGenerator`)

Produz documento formato **A4 (`595 x 842 pt`)** com multiplas paginas automaticas:
- **Cabecalho Institucional:** Identificacao visual OpenSintegra Contabil, titulo e periodo filtrado.
- **Identificacao da Empresa:** Razao Social, CNPJ formatado, Inscricao Estadual, Regime Tributario e Versao do Convenio ICMS 57/95.
- **Resumo do Periodo (4 Blocos KPI):** Total de Entradas, Total de Saidas, Saldo (`Entradas` x `Saidas`) e Equilibrio Fiscal percentual.
- **Movimentacao Mensal:** Tabela zebrada exibindo `Compras`, `Outras Entradas`, `Total Entradas`, `Total Saidas`, `Saldo (S - E)`, `Proporcao S/E` e linha consolidada de Total com calculo proporcional global.
- **Observacoes e Pendencias:** Lista de inconsistencias Sintegra no periodo e XMLs de compra omissos.
- **Pagina Complementar (quando houver dados):** Detalhamento de Notas Fiscais Faltantes (Confronto XML) e Apuracao por CFOP.

## 2. Pasta de Trabalho Excel (`ExcelReportGenerator`)

Gera arquivo `.xlsx` contendo **7 planilhas organizadas e estilizadas**:

| Aba | Conteudo Principal |
| --- | ------------------ |
| `1. Resumo Executivo` | Dados cadastrais da empresa, periodo avaliado, regime tributario, totais consolidados e saldo |
| `2. Movimentacao Mensal` | Evolucao mensal completa com totais calculados via formula dinamica `SUM(...)` e proporcao S/E |
| `3. Apuracao por CFOP` | Sumarizacao contabil e tributaria (Valor Contabil, Base ICMS, ICMS, Isentas, Outras) |
| `4. Notas Reg 50` | Listagem das notas fiscais (Registro 50) presentes no escopo selecionado |
| `5. Saidas NFC-e Reg 61` | Movimentacao diaria por documento fiscal de consumidor / NFC-e (Registro 61) |
| `6. Confronto XML` | Notas XML auditadas com destaque visual para compras omissas |
| `7. Diagnostico` | Listagem de erros, avisos e informacoes de validacao apontados no arquivo |
