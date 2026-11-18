# Geração de Relatórios Vetoriais (PDF) e Planilhas Excel (.xlsx)

O módulo `OpenSintegra.Reports` utiliza exclusivamente bibliotecas permissivas de código aberto (`SkiaSharp` e `ClosedXML`), funcionando de forma idêntica no Windows, Linux e macOS sem depender de Microsoft Office ou suítes proprietárias instaladas.

Todos os relatórios respeitam integralmente o filtro ativo na tela ou no parâmetro `--month YYYYMM` da CLI através de `ScopedFiscalView`.

## 1. Relatório Vetorial em PDF (`PdfReportGenerator`)

Produz documento formato **A4 (`595 x 842 pt`)** com múltiplas páginas automáticas em três modos (`Completo`, `SomenteNotasNaoLancadas` e `ProdutosMaisVendidos`):
- **Cabeçalho Institucional:** Monograma vetorial OpenSintegra Contábil, título do relatório e período filtrado.
- **Identificação da Empresa:** Razão Social, CNPJ formatado, Inscrição Estadual, Regime Tributário e Convênio ICMS 57/95.
- **Resumo do Período (4 Blocos KPI):** Total de Entradas, Total de Saídas (Vendas), Saídas / Entradas e Compras Não Lançadas (XML).
- **Movimentação Mês a Mês:** Tabela zebrada exibindo `Compras`, `Outras Entr.`, `Total Entradas`, `Total Saídas`, `Saídas / Entr.`, `Conferência XML` e linha consolidada de Total.
- **Notas Fiscais de Compra Não Lançadas:** Listagem em linguagem simples com mês, emissão, número da NF, fornecedor e valor faltante para envio ao cliente.
- **Produtos Mais Vendidos (Top 40):** Ranking por valor movimentado com código, descrição (`Reg 75`), unidade, quantidade, total vendido e participação percentual.

## 2. Pasta de Trabalho Excel (`ExcelReportGenerator`)

Gera arquivo `.xlsx` contendo **8 planilhas organizadas e estilizadas**:

| Aba | Conteúdo Principal |
| --- | ------------------ |
| `Resumo Executivo` | Dados cadastrais da empresa, período avaliado, regime tributário e totais consolidados |
| `Movimentação Mensal` | Evolução mensal completa com totais calculados via fórmula dinâmica `SUM(...)` e proporção S/E |
| `Produtos Mais Vendidos` | Ranking de mercadorias movimentadas com quantidade, total vendido e participação (%) |
| `Apuração por CFOP` | Sumarização contábil e tributária (Valor Contábil, Base ICMS, ICMS, Isentas, Outras) |
| `Notas Reg 50` | Listagem das notas fiscais (Registro 50) presentes no escopo selecionado |
| `Saídas NFC-e Reg 61` | Movimentação diária por documento fiscal de consumidor / NFC-e (Registro 61) |
| `Confronto XML` | Notas XML auditadas com destaque visual para compras não lançadas |
| `Diagnóstico` | Listagem de erros, avisos e informações de validação apontados no arquivo |
