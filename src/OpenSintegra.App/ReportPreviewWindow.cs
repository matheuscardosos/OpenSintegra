using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using OpenSintegra.Core;
using OpenSintegra.Reports;

namespace OpenSintegra.App;

/// <summary>
/// Document preview window supporting client-ready full summary, omitted XML invoices report,
/// and top-selling products report, with native vector PDF and Excel export.
/// </summary>
public sealed class ReportPreviewWindow : Window
{
    private readonly FiscalReportContext _baseCtx;
    private FiscalReportContext _activeCtx;
    private readonly LayoutTransformControl _zoomHost = new();
    private double _zoomScale = 1.0;

    public ReportPreviewWindow(FiscalReportContext ctx)
    {
        _baseCtx = ctx;
        _activeCtx = ctx;
        Title = $"OpenSintegra Contábil v{ctx.AppVersion} - Relatório";
        Icon = VectorIcons.AppWindowIcon;
        Width = 980;
        Height = 760;
        MinWidth = 780;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#E2E8F0");

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };

        var toolbar = BuildToolbar();
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Padding = new Thickness(28, 22)
        };

        _zoomHost.Child = BuildPaperSheet();
        _zoomHost.LayoutTransform = new ScaleTransform(_zoomScale, _zoomScale);
        scroll.Content = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = _zoomHost
        };

        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        Content = root;
    }

    private Control BuildToolbar()
    {
        var bar = new Border
        {
            Background = Brush.Parse("#F8FAFC"),
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 9)
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };

        var leftBtns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var btnPrint = CreateToolbarButton("Imprimir", VectorIcons.DocumentText, "#334155");
        btnPrint.Click += (_, _) => PrintViaTempPdf();

        var btnPdf = CreateToolbarButton("Salvar PDF", VectorIcons.ExportDoc, "#DC2626");
        btnPdf.Click += async (_, _) => await ExportPdfAsync();

        var btnXlsx = CreateToolbarButton("Salvar Excel (.xlsx)", VectorIcons.ChartBar, "#15803D");
        btnXlsx.Click += async (_, _) => await ExportXlsxAsync();

        leftBtns.Children.Add(btnPrint);
        leftBtns.Children.Add(btnPdf);
        leftBtns.Children.Add(btnXlsx);
        Grid.SetColumn(leftBtns, 0);
        grid.Children.Add(leftBtns);

        // Mode Switcher & Zoom Controls
        var centerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(16, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var cmbMode = new ComboBox { Width = 290, FontSize = 12 };
        cmbMode.Items.Add(new ComboBoxItem { Content = "Relatório Completo (Entradas x Saídas + Faltantes)", Tag = ReportMode.Completo });
        cmbMode.Items.Add(new ComboBoxItem { Content = "Somente Notas Não Lançadas (Para Cliente)", Tag = ReportMode.SomenteNotasNaoLancadas });
        cmbMode.Items.Add(new ComboBoxItem { Content = "Produtos Mais Vendidos", Tag = ReportMode.ProdutosMaisVendidos });
        cmbMode.SelectedIndex = _baseCtx.Mode switch
        {
            ReportMode.SomenteNotasNaoLancadas => 1,
            ReportMode.ProdutosMaisVendidos => 2,
            _ => 0
        };
        cmbMode.SelectionChanged += (_, _) =>
        {
            if (cmbMode.SelectedItem is ComboBoxItem cbi && cbi.Tag is ReportMode mode)
            {
                _activeCtx = _baseCtx with { Mode = mode };
                _zoomHost.Child = BuildPaperSheet();
            }
        };
        centerPanel.Children.Add(cmbMode);

        var cmbZoom = new ComboBox
        {
            Width = 88,
            ItemsSource = new[] { "75%", "100%", "125%", "150%" },
            SelectedIndex = 1,
            FontSize = 12
        };
        cmbZoom.SelectionChanged += (_, _) =>
        {
            string? sel = cmbZoom.SelectedItem as string;
            _zoomScale = sel switch
            {
                "75%" => 0.75,
                "125%" => 1.25,
                "150%" => 1.50,
                _ => 1.0
            };
            _zoomHost.LayoutTransform = new ScaleTransform(_zoomScale, _zoomScale);
        };
        centerPanel.Children.Add(cmbZoom);

        Grid.SetColumn(centerPanel, 1);
        grid.Children.Add(centerPanel);

        var btnClose = new Button
        {
            Content = "Fechar",
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            Foreground = Brush.Parse("#0F172A"),
            Padding = new Thickness(16, 6),
            CornerRadius = new CornerRadius(5)
        };
        btnClose.Click += (_, _) => Close();
        Grid.SetColumn(btnClose, 3);
        grid.Children.Add(btnClose);

        bar.Child = grid;
        return bar;
    }

    private static Button CreateToolbarButton(string label, string iconPath, string iconColor)
    {
        return new Button
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            Foreground = Brush.Parse("#0F172A"),
            Padding = new Thickness(12, 6),
            CornerRadius = new CornerRadius(5),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    VectorIcons.Create(iconPath, iconColor, 15),
                    new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
    }

    private Control BuildPaperSheet()
    {
        var v = _activeCtx.ScopedView;
        var r10 = _activeCtx.Document.Reg10;

        var sheet = new Border
        {
            Width = 800,
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(42, 36),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetY = 4,
                Blur = 16,
                Color = Color.FromArgb(40, 15, 23, 42)
            })
        };

        var stack = new StackPanel { Spacing = 16 };

        string docTitle = _activeCtx.Mode switch
        {
            ReportMode.SomenteNotasNaoLancadas => "Notas Fiscais Não Lançadas no Sintegra",
            ReportMode.ProdutosMaisVendidos => "Ranking de Produtos Mais Vendidos",
            _ => "Relatório Fiscal de Entradas e Saídas"
        };

        // Top Report Header
        var hdrGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var brandRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                VectorIcons.CreateBrandMark(34, "#0066FC"),
                new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock { Text = "OpenSintegra", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#0F172A") },
                        new TextBlock { Text = "Contábil", FontSize = 14, Foreground = Brush.Parse("#475569") }
                    }
                }
            }
        };
        Grid.SetColumn(brandRow, 0);
        hdrGrid.Children.Add(brandRow);

        var titleRight = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = docTitle, FontSize = 17, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#0F172A"), HorizontalAlignment = HorizontalAlignment.Right },
                new TextBlock { Text = $"Período: {v.PeriodLabel}", FontSize = 13, Foreground = Brush.Parse("#475569"), HorizontalAlignment = HorizontalAlignment.Right }
            }
        };
        Grid.SetColumn(titleRight, 1);
        hdrGrid.Children.Add(titleRight);
        stack.Children.Add(hdrGrid);
        stack.Children.Add(new Border { Height = 2, Background = Brush.Parse("#1E293B") });

        // Company Section
        stack.Children.Add(CreateBand("Identificação da Empresa"));
        var compGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,250"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        compGrid.Children.Add(CreateTextLine($"Razão Social: {r10?.NomeContribuinte ?? "-"}", true, 0, 0));
        compGrid.Children.Add(CreateTextLine($"Regime Tributário: {_activeCtx.TaxRegime}", false, 1, 0));
        compGrid.Children.Add(CreateTextLine($"CNPJ: {(r10 is null ? "-" : TaxIdValidator.FormatCnpj(r10.Cnpj))}   |   IE: {r10?.Ie ?? "-"}", false, 0, 1));
        compGrid.Children.Add(CreateTextLine("Documento: Sintegra (Convênio ICMS 57/95)", false, 1, 1));
        stack.Children.Add(compGrid);

        // Resumo do Periodo
        stack.Children.Add(CreateBand("Resumo do Período"));
        var summaryBox = new Border
        {
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            ClipToBounds = true
        };
        var sGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        sGrid.Children.Add(CreateSummaryCell("Total de Entradas", $"R$ {TaxIdValidator.FormatCentsBrl(v.TotalEntradasCents)}", "#FFFFFF", "#0F172A", 0, true));
        sGrid.Children.Add(CreateSummaryCell("Total de Saídas (Vendas)", $"R$ {TaxIdValidator.FormatCentsBrl(v.TotalSaidasCents)}", "#FFFFFF", "#0F172A", 1, true));
        sGrid.Children.Add(CreateSummaryCell("Saídas / Entradas",
            v.RatioSaidaSobreEntradaPct.HasValue ? $"{v.RatioSaidaSobreEntradaPct.Value:N1}%" : "-",
            "#FFFFFF", "#1D4ED8", 2, true));

        bool hasMissing = _activeCtx.Audit is not null && v.XmlComprasOmissas > 0;
        string missText = _activeCtx.Audit is null
            ? "XML não carregado"
            : (hasMissing ? $"{v.XmlComprasOmissas} (R$ {TaxIdValidator.FormatCentsBrl(v.XmlComprasOmissasCents)})" : "Todas lançadas");
        sGrid.Children.Add(CreateSummaryCell("Compras Não Lançadas (XML)",
            missText,
            _activeCtx.Audit is null ? "#FFFFFF" : (hasMissing ? "#FEF2F2" : "#ECFDF5"),
            _activeCtx.Audit is null ? "#64748B" : (hasMissing ? "#B91C1C" : "#15803D"), 3, false));
        summaryBox.Child = sGrid;
        stack.Children.Add(summaryBox);

        // Monthly table (for Completo)
        if (_activeCtx.Mode == ReportMode.Completo)
        {
            stack.Children.Add(CreateBand("Movimentação Mês a Mês (Entradas x Saídas x Conferência XML)"));
            var tableBorder = new Border
            {
                BorderBrush = Brush.Parse("#CBD5E1"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                ClipToBounds = true
            };
            var tStack = new StackPanel();
            string cols = "65,98,90,98,98,84,*";
            var tHdr = new Grid { ColumnDefinitions = new ColumnDefinitions(cols) };
            tHdr.Children.Add(CreateCell("Mês", true, 0, false));
            tHdr.Children.Add(CreateCell("Compras", true, 1, true));
            tHdr.Children.Add(CreateCell("Outras Ent.", true, 2, true));
            tHdr.Children.Add(CreateCell("Total Entradas", true, 3, true));
            tHdr.Children.Add(CreateCell("Total Saídas", true, 4, true));
            tHdr.Children.Add(CreateCell("Saídas / Entr.", true, 5, true));
            tHdr.Children.Add(CreateCell("Conferência XML", true, 6, true));
            tStack.Children.Add(new Border { Background = Brush.Parse("#F1F5F9"), Padding = new Thickness(10, 8), Child = tHdr });

            foreach (var m in v.MonthlyRows)
            {
                var r = new Grid { ColumnDefinitions = new ColumnDefinitions(cols) };
                r.Children.Add(CreateCell(m.MonthLabel, false, 0, false));
                r.Children.Add(CreateCell($"R$ {TaxIdValidator.FormatCentsBrl(m.ComprasEntradasCents)}", false, 1, true));
                r.Children.Add(CreateCell($"R$ {TaxIdValidator.FormatCentsBrl(m.OutrasEntradasCents)}", false, 2, true));
                r.Children.Add(CreateCell($"R$ {TaxIdValidator.FormatCentsBrl(m.TotalEntradasCents)}", true, 3, true));
                r.Children.Add(CreateCell($"R$ {TaxIdValidator.FormatCentsBrl(m.TotalSaidasCents)}", true, 4, true));
                var prCell = CreateCell(m.SaidaSobreEntradaPct.HasValue ? $"{m.SaidaSobreEntradaPct.Value:N1}%" : "-", true, 5, true);
                prCell.Foreground = Brush.Parse("#1D4ED8");
                r.Children.Add(prCell);

                string xmlLbl = "Sem XML";
                string xmlColor = "#64748B";
                if (_activeCtx.Audit is not null)
                {
                    if (v.XmlCoverageByMonth.TryGetValue(m.YearMonth, out var cov) && cov.XmlComprasOmissas > 0)
                    {
                        xmlLbl = $"{cov.XmlComprasOmissas} falta (R$ {TaxIdValidator.FormatCentsBrl(cov.XmlComprasOmissasCents)})";
                        xmlColor = "#B91C1C";
                    }
                    else
                    {
                        xmlLbl = "Todas lançadas";
                        xmlColor = "#15803D";
                    }
                }
                var xCell = CreateCell(xmlLbl, true, 6, true);
                xCell.Foreground = Brush.Parse(xmlColor);
                r.Children.Add(xCell);

                tStack.Children.Add(new Border
                {
                    BorderBrush = Brush.Parse("#E2E8F0"),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(10, 7),
                    Child = r
                });
            }

            var tTot = new Grid { ColumnDefinitions = new ColumnDefinitions(cols) };
            tTot.Children.Add(CreateCell("Total", true, 0, false));
            tTot.Children.Add(CreateCell($"R$ {TaxIdValidator.FormatCentsBrl(v.ComprasEntradasCents)}", true, 1, true));
            tTot.Children.Add(CreateCell($"R$ {TaxIdValidator.FormatCentsBrl(v.OutrasEntradasCents)}", true, 2, true));
            tTot.Children.Add(CreateCell($"R$ {TaxIdValidator.FormatCentsBrl(v.TotalEntradasCents)}", true, 3, true));
            tTot.Children.Add(CreateCell($"R$ {TaxIdValidator.FormatCentsBrl(v.TotalSaidasCents)}", true, 4, true));
            tTot.Children.Add(CreateCell(v.RatioSaidaSobreEntradaPct.HasValue ? $"{v.RatioSaidaSobreEntradaPct.Value:N1}%" : "-", true, 5, true));
            var totX = CreateCell(_activeCtx.Audit is null ? "-" : $"{v.XmlComprasOmissas} falta(s)", true, 6, true);
            totX.Foreground = Brush.Parse(hasMissing ? "#B91C1C" : "#15803D");
            tTot.Children.Add(totX);

            tStack.Children.Add(new Border
            {
                Background = Brush.Parse("#F1F5F9"),
                BorderBrush = Brush.Parse("#CBD5E1"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(10, 8),
                Child = tTot
            });
            tableBorder.Child = tStack;
            stack.Children.Add(tableBorder);
        }

        // Omitted Purchase Invoices Table (Completo + SomenteNotasNaoLancadas)
        if (_activeCtx.Mode is ReportMode.Completo or ReportMode.SomenteNotasNaoLancadas)
        {
            stack.Children.Add(CreateBand("Notas Fiscais de Compra Não Lançadas no Sintegra"));
            var missingRows = v.XmlDocumentRows.Where(r => r.IsCriticalGap).ToList();
            if (_activeCtx.Audit is null)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "Nenhum arquivo XML foi importado para conferir notas não lançadas.",
                    FontSize = 12,
                    Foreground = Brush.Parse("#64748B")
                });
            }
            else if (missingRows.Count == 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "Todas as notas fiscais de compra encontradas nos XMLs do período constam lançadas no Sintegra.",
                    FontSize = 12.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush.Parse("#15803D")
                });
            }
            else
            {
                var mBorder = new Border { BorderBrush = Brush.Parse("#FECACA"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), ClipToBounds = true };
                var mStack = new StackPanel();
                string mCols = "68,85,95,*,115";
                var mHdr = new Grid { ColumnDefinitions = new ColumnDefinitions(mCols) };
                mHdr.Children.Add(CreateCell("Mês", true, 0, false));
                mHdr.Children.Add(CreateCell("Emissão", true, 1, false));
                mHdr.Children.Add(CreateCell("Número NF", true, 2, false));
                mHdr.Children.Add(CreateCell("Fornecedor", true, 3, false));
                mHdr.Children.Add(CreateCell("Valor NF", true, 4, true));
                mStack.Children.Add(new Border { Background = Brush.Parse("#FEF2F2"), Padding = new Thickness(10, 7), Child = mHdr });

                foreach (var miss in missingRows)
                {
                    var mr = new Grid { ColumnDefinitions = new ColumnDefinitions(mCols) };
                    mr.Children.Add(CreateCell(TaxIdValidator.FormatMonthLabel(miss.YearMonth), false, 0, false));
                    mr.Children.Add(CreateCell(miss.DataBr, false, 1, false));
                    var numCell = CreateCell($"{miss.Numero}/{miss.Serie}", true, 2, false);
                    numCell.Foreground = Brush.Parse("#B91C1C");
                    mr.Children.Add(numCell);
                    mr.Children.Add(CreateCell(miss.EmitNome, false, 3, false));
                    var vCell = CreateCell($"R$ {TaxIdValidator.FormatCentsBrl(miss.XmlValorCents)}", true, 4, true);
                    vCell.Foreground = Brush.Parse("#B91C1C");
                    mr.Children.Add(vCell);
                    mStack.Children.Add(new Border
                    {
                        BorderBrush = Brush.Parse("#E2E8F0"),
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        Padding = new Thickness(10, 6),
                        Child = mr
                    });
                }
                mBorder.Child = mStack;
                stack.Children.Add(mBorder);
            }
        }

        // Top-Selling Products Table
        if (_activeCtx.Mode == ReportMode.ProdutosMaisVendidos && v.TopSellingProducts.Count > 0)
        {
            stack.Children.Add(CreateBand("Produtos Mais Vendidos / Movimentados (Top 40)"));
            var pBorder = new Border { BorderBrush = Brush.Parse("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), ClipToBounds = true };
            var pStack = new StackPanel();
            string pCols = "40,95,*,50,80,115,65";
            var pHdr = new Grid { ColumnDefinitions = new ColumnDefinitions(pCols) };
            pHdr.Children.Add(CreateCell("#", true, 0, false));
            pHdr.Children.Add(CreateCell("Código", true, 1, false));
            pHdr.Children.Add(CreateCell("Produto", true, 2, false));
            pHdr.Children.Add(CreateCell("Unid.", true, 3, false));
            pHdr.Children.Add(CreateCell("Qtd.", true, 4, true));
            pHdr.Children.Add(CreateCell("Total Vendido", true, 5, true));
            pHdr.Children.Add(CreateCell("% Part.", true, 6, true));
            pStack.Children.Add(new Border { Background = Brush.Parse("#F1F5F9"), Padding = new Thickness(10, 7), Child = pHdr });

            int rk = 1;
            foreach (var prod in v.TopSellingProducts.Take(40))
            {
                var pr = new Grid { ColumnDefinitions = new ColumnDefinitions(pCols) };
                pr.Children.Add(CreateCell($"{rk++}.", false, 0, false));
                pr.Children.Add(CreateCell(prod.CodProduto, false, 1, false));
                pr.Children.Add(CreateCell(prod.Descricao, false, 2, false));
                pr.Children.Add(CreateCell(prod.Unidade, false, 3, false));
                pr.Children.Add(CreateCell(TaxIdValidator.FormatMilliBr(prod.QuantidadeMilli), false, 4, true));
                pr.Children.Add(CreateCell($"R$ {TaxIdValidator.FormatCentsBrl(prod.ValorTotalCents)}", true, 5, true));
                pr.Children.Add(CreateCell($"{prod.SharePct:N1}%", false, 6, true));
                pStack.Children.Add(new Border
                {
                    BorderBrush = Brush.Parse("#E2E8F0"),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(10, 6),
                    Child = pr
                });
            }
            pBorder.Child = pStack;
            stack.Children.Add(pBorder);
        }

        // Footer
        stack.Children.Add(new Border { Height = 1, Background = Brush.Parse("#CBD5E1"), Margin = new Thickness(0, 10, 0, 0) });
        var footGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        footGrid.Children.Add(new TextBlock
        {
            Text = $"OpenSintegra Contábil v{_activeCtx.AppVersion}",
            FontSize = 11.5,
            Foreground = Brush.Parse("#64748B")
        });
        var dtText = new TextBlock
        {
            Text = $"Relatório gerado em {DateTime.Now:dd/MM/yyyy} às {DateTime.Now:HH:mm}",
            FontSize = 11.5,
            Foreground = Brush.Parse("#64748B")
        };
        Grid.SetColumn(dtText, 1);
        footGrid.Children.Add(dtText);
        stack.Children.Add(footGrid);

        sheet.Child = stack;
        return sheet;
    }

    private static Border CreateBand(string text) => new()
    {
        Background = Brush.Parse("#E2E8F0"),
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(10, 5),
        Child = new TextBlock { Text = text, FontWeight = FontWeight.Bold, FontSize = 12.5, Foreground = Brush.Parse("#0F172A") }
    };

    private static TextBlock CreateTextLine(string text, bool bold, int col, int row)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
            FontSize = 12.5,
            Foreground = Brush.Parse("#0F172A"),
            Margin = new Thickness(4, 2)
        };
        Grid.SetColumn(tb, col);
        Grid.SetRow(tb, row);
        return tb;
    }

    private static Border CreateSummaryCell(string label, string val, string bgHex, string fgHex, int col, bool rightBorder)
    {
        var b = new Border
        {
            Background = Brush.Parse(bgHex),
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(0, 0, rightBorder ? 1 : 0, 0),
            Padding = new Thickness(12, 10),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = label, FontSize = 11, Foreground = Brush.Parse("#475569") },
                    new TextBlock { Text = val, FontSize = 14, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(fgHex) }
                }
            }
        };
        Grid.SetColumn(b, col);
        return b;
    }

    private static TextBlock CreateCell(string text, bool bold, int col, bool alignRight)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 11.5,
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = Brush.Parse("#0F172A"),
            TextAlignment = alignRight ? TextAlignment.Right : TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(tb, col);
        return tb;
    }

    private async Task ExportPdfAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Relatório em PDF",
            SuggestedFileName = $"Relatorio_Sintegra_{_activeCtx.ScopedView.SelectedYearMonth ?? "Anual"}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { new FilePickerFileType("Documento PDF") { Patterns = new[] { "*.pdf" } } }
        });
        if (file is null) return;
        string? path = file.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            PdfReportGenerator.GeneratePdf(_activeCtx, path);
    }

    private async Task ExportXlsxAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Planilha Excel (.xlsx)",
            SuggestedFileName = $"Planilha_Sintegra_{_activeCtx.ScopedView.SelectedYearMonth ?? "Anual"}.xlsx",
            DefaultExtension = "xlsx",
            FileTypeChoices = new[] { new FilePickerFileType("Planilha Excel") { Patterns = new[] { "*.xlsx" } } }
        });
        if (file is null) return;
        string? path = file.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            ExcelReportGenerator.GenerateWorkbook(_activeCtx, path);
    }

    private void PrintViaTempPdf()
    {
        try
        {
            string tempPdf = Path.Combine(Path.GetTempPath(), $"OpenSintegra_Relatorio_{Guid.NewGuid():N}.pdf");
            PdfReportGenerator.GeneratePdf(_activeCtx, tempPdf);
            Process.Start(new ProcessStartInfo(tempPdf) { UseShellExecute = true });
        }
        catch
        {
            // Ignore OS launcher issues on headless environments
        }
    }
}
