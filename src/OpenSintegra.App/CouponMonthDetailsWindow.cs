using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using OpenSintegra.Core;

namespace OpenSintegra.App;

/// <summary>
/// Shows daily coupon summary details (Reg 61) alongside all products sold via consumer coupons
/// in that month (Reg 61R joined with Reg 75 product descriptions), with a raw inspector fallback.
/// </summary>
public sealed class CouponMonthDetailsWindow : Window
{
    public CouponMonthDetailsWindow(Reg61 couponDay, SintegraDocument doc)
    {
        string yyyyMm = couponDay.DataEmissao.Length >= 6 ? couponDay.DataEmissao[..6] : "";
        string mmYyyy = yyyyMm.Length == 6 ? $"{yyyyMm[4..6]}{yyyyMm[..4]}" : "";
        string monthLabel = yyyyMm.Length == 6 ? TaxIdValidator.FormatMonthLabel(yyyyMm) : "-";

        Title = $"Cupons NFC-e / Consumidor - {TaxIdValidator.FormatDateBr(couponDay.DataEmissao)} (Mês {monthLabel})";
        Icon = VectorIcons.AppWindowIcon;
        Width = 900;
        Height = 640;
        MinWidth = 720;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#EEF2F6");

        var r75Map = new Dictionary<string, Reg75>(StringComparer.Ordinal);
        foreach (var p in doc.Reg75)
            r75Map.TryAdd(p.CodProduto.Trim(), p);

        var monthProducts = doc.Reg61R
            .Where(r => r.MesAno == mmYyyy)
            .OrderByDescending(r => r.ValorBrutoCents)
            .ToList();

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(18)
        };

        // Top Summary Card for Selected Reg 61 Day
        var headerCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 14),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var hStack = new StackPanel { Spacing = 10 };
        var iniNum = couponDay.NumInicialOrdem.TrimStart('0').PadLeft(1, '0');
        var fimNum = couponDay.NumFinalOrdem.TrimStart('0').PadLeft(1, '0');
        long countEst = (long.TryParse(iniNum, out long n1) && long.TryParse(fimNum, out long n2) && n2 >= n1)
            ? (n2 - n1 + 1)
            : 1;

        var topTitleGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        topTitleGrid.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = $"Cupons NFC-e do Dia {TaxIdValidator.FormatDateBr(couponDay.DataEmissao)}  |  Modelo {couponDay.Modelo}  |  Série '{couponDay.Serie.Trim()}'",
                    FontSize = 15,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush.Parse("#0F172A")
                },
                new TextBlock
                {
                    Text = $"Numeração de {iniNum} até {fimNum} ({countEst} cupom(ns) nesta faixa diária)",
                    FontSize = 12,
                    Foreground = Brush.Parse("#475569")
                }
            }
        });

        var monthPill = new Border
        {
            Background = Brush.Parse("#DCFCE7"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"Saída Consumidor ({monthLabel})",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#15803D")
            }
        };
        Grid.SetColumn(monthPill, 1);
        topTitleGrid.Children.Add(monthPill);
        hStack.Children.Add(topTitleGrid);

        var kvGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        void AddKv(int col, string k, string v, string? hex = null)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 2, 12, 2) };
            sp.Children.Add(new TextBlock { Text = k, FontSize = 11, Foreground = Brush.Parse("#475569") });
            sp.Children.Add(new TextBlock { Text = v, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse(hex ?? "#0F172A") });
            Grid.SetColumn(sp, col);
            kvGrid.Children.Add(sp);
        }

        AddKv(0, "Valor Total do Dia", $"R$ {TaxIdValidator.FormatCentsBrl(couponDay.ValorTotalCents)}", "#15803D");
        AddKv(1, "Base de Cálculo ICMS", $"R$ {TaxIdValidator.FormatCentsBrl(couponDay.BaseIcmsCents)}");
        AddKv(2, "Valor do ICMS", $"R$ {TaxIdValidator.FormatCentsBrl(couponDay.ValorIcmsCents)}");
        AddKv(3, "Alíquota / Isentas", $"{couponDay.AliquotaBps / 100.0:N2}%  |  R$ {TaxIdValidator.FormatCentsBrl(couponDay.IsentaCents)}");
        hStack.Children.Add(kvGrid);

        hStack.Children.Add(new Border
        {
            Background = Brush.Parse("#EFF6FF"),
            BorderBrush = Brush.Parse("#BFDBFE"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6),
            Child = new TextBlock
            {
                Text = "Regra do Sintegra (Convênio ICMS 57/95): o Registro 61 agrupa os cupons NFC-e por dia (sem lista de itens por cupom individual). Os produtos vendidos nos cupons ficam detalhados no resumo mensal do Registro 61R exibido abaixo.",
                FontSize = 11.5,
                Foreground = Brush.Parse("#1E3A8A"),
                TextWrapping = TextWrapping.Wrap
            }
        });

        headerCard.Child = hStack;
        Grid.SetRow(headerCard, 0);
        root.Children.Add(headerCard);

        // Middle: Reg 61R Products for this Month
        var prodCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12)
        };

        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        var topSearchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 8) };
        long sumMonthCents = monthProducts.Sum(x => x.ValorBrutoCents);
        var lblHeader = new TextBlock
        {
            Text = $"Produtos Vendidos nos Cupons em {monthLabel} (Registro 61R: {monthProducts.Count} item(ns) | Total Mês: R$ {TaxIdValidator.FormatCentsBrl(sumMonthCents)})",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#0F172A"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var txtSearch = new TextBox
        {
            Watermark = "Filtrar código ou descrição...",
            Width = 230,
            FontSize = 11.5
        };
        Grid.SetColumn(lblHeader, 0);
        Grid.SetColumn(txtSearch, 1);
        topSearchRow.Children.Add(lblHeader);
        topSearchRow.Children.Add(txtSearch);
        layout.Children.Add(topSearchRow);

        var colLens = new[]
        {
            new GridLength(85), new GridLength(2.0, GridUnitType.Star),
            new GridLength(48), new GridLength(82), new GridLength(100),
            new GridLength(95), new GridLength(60)
        };

        void AddCell(Grid g, int col, string text, bool right, bool bold, string hex)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
                Foreground = Brush.Parse(hex),
                TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
                Padding = new Thickness(6, 5),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }

        var hdr = UiTableHelpers.BuildRowGrid(colLens);
        AddCell(hdr, 0, "Código", false, true, "#334155");
        AddCell(hdr, 1, "Descrição do Produto (Registro 75)", false, true, "#334155");
        AddCell(hdr, 2, "Unid.", false, true, "#334155");
        AddCell(hdr, 3, "Qtd. Mês", true, true, "#334155");
        AddCell(hdr, 4, "Total Vendido", true, true, "#334155");
        AddCell(hdr, 5, "Base ICMS", true, true, "#334155");
        AddCell(hdr, 6, "Alíq.", true, true, "#334155");

        var hdrBorder = new Border
        {
            Background = Brush.Parse("#F1F5F9"),
            BorderBrush = Brush.Parse("#E2E8F0"),
            BorderThickness = new Thickness(1, 1, 1, 0),
            Child = hdr
        };
        Grid.SetRow(hdrBorder, 1);
        layout.Children.Add(hdrBorder);

        if (monthProducts.Count == 0)
        {
            var empty = new Border
            {
                BorderBrush = Brush.Parse("#E2E8F0"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Child = new TextBlock
                {
                    Text = $"Nenhum Registro 61R (resumo mensal de produtos vendidos por cupom) encontrado para {monthLabel}.",
                    FontSize = 12,
                    Foreground = Brush.Parse("#475569"),
                    TextWrapping = TextWrapping.Wrap
                }
            };
            Grid.SetRow(empty, 2);
            layout.Children.Add(empty);
        }
        else
        {
            var pad = new Thickness(6, 5);
            var lb = UiTableHelpers.CreateRecycledListBox<Reg61R>(
                monthProducts,
                colLens,
                (g, slots, it) =>
                {
                    r75Map.TryGetValue(it.CodProduto.Trim(), out var r75);
                    string desc = r75?.Descricao ?? $"PRODUTO {it.CodProduto}";
                    string un = r75?.Unidade ?? "-";
                    UiTableHelpers.SetCellSlot(g, slots, 0, it.CodProduto, TextAlignment.Left, FontWeight.Normal, Brush.Parse("#334155"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 1, desc, TextAlignment.Left, FontWeight.Normal, Brush.Parse("#0F172A"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 2, un, TextAlignment.Left, FontWeight.Normal, Brush.Parse("#475569"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 3, TaxIdValidator.FormatMilliBr(it.QuantidadeMilli), TextAlignment.Right, FontWeight.Normal, Brush.Parse("#334155"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 4, $"R$ {TaxIdValidator.FormatCentsBrl(it.ValorBrutoCents)}", TextAlignment.Right, FontWeight.SemiBold, Brush.Parse("#15803D"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 5, $"R$ {TaxIdValidator.FormatCentsBrl(it.BaseIcmsCents)}", TextAlignment.Right, FontWeight.Normal, Brush.Parse("#475569"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 6, $"{it.AliquotaBps / 100.0:N1}%", TextAlignment.Right, FontWeight.Normal, Brush.Parse("#475569"), 11.5, pad);
                },
                borderThickness: new Thickness(1),
                borderBrush: Brush.Parse("#E2E8F0"));
            UiTableHelpers.AttachSyncedHeaderPadding(lb, hdr);

            txtSearch.TextChanged += (_, _) =>
            {
                string q = txtSearch.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(q))
                {
                    lb.ItemsSource = monthProducts;
                    return;
                }
                lb.ItemsSource = monthProducts.Where(it =>
                {
                    r75Map.TryGetValue(it.CodProduto.Trim(), out var r75);
                    return it.CodProduto.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                           (r75?.Descricao.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
                }).ToList();
            };

            Grid.SetRow(lb, 2);
            layout.Children.Add(lb);
        }

        prodCard.Child = layout;
        Grid.SetRow(prodCard, 1);
        root.Children.Add(prodCard);

        // Footer Buttons
        var foot = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 12, 0, 0)
        };

        var btnRaw = new Button
        {
            Content = $"Inspecionar colunas do Registro 61 (Linha {couponDay.Line})",
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            Foreground = Brush.Parse("#334155"),
            Padding = new Thickness(12, 7),
            CornerRadius = new CornerRadius(5)
        };
        btnRaw.Click += async (_, _) =>
        {
            if (couponDay.Line >= 1 && couponDay.Line <= doc.Lines.Count)
            {
                var insp = new LineInspectorWindow(doc.Lines[couponDay.Line - 1], 1, 126);
                await insp.ShowDialog(this);
            }
        };
        Grid.SetColumn(btnRaw, 0);
        foot.Children.Add(btnRaw);

        var btnClose = new Button
        {
            Content = "Fechar",
            Background = Brush.Parse("#2563EB"),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(20, 7),
            CornerRadius = new CornerRadius(5)
        };
        btnClose.Click += (_, _) => Close();
        Grid.SetColumn(btnClose, 2);
        foot.Children.Add(btnClose);

        Grid.SetRow(foot, 2);
        root.Children.Add(foot);

        Content = root;
        UiTableHelpers.ConfigureModalShortcuts(this, txtSearch);
    }
}
