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
/// Shows human-readable invoice header, CFOP classification, fiscal totals and Reg 54 items
/// (joined with Reg 75 product descriptions), with a discrete option to inspect raw columns.
/// </summary>
public sealed class InvoiceDetailsWindow : Window
{
    public InvoiceDetailsWindow(
        Reg50 invoice,
        SintegraDocument doc,
        XmlAuditDocumentRow? xmlMatch = null)
    {
        string numDisplay = invoice.Numero.TrimStart('0').PadLeft(1, '0');
        Title = $"Detalhes da Nota Fiscal {numDisplay} (Série {invoice.Serie.Trim()})";
        Icon = VectorIcons.AppWindowIcon;
        Width = 880;
        Height = 620;
        MinWidth = 720;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#EEF2F6");

        var r75Map = new Dictionary<string, Reg75>(StringComparer.Ordinal);
        foreach (var p in doc.Reg75)
            r75Map.TryAdd(p.CodProduto, p);

        static string NormKey(string cnpj, string mod, string ser, string num) =>
            $"{cnpj.Trim()}|{mod.Trim()}|{ser.Trim().TrimStart('0')}|{num.Trim().TrimStart('0')}";

        string targetKey = NormKey(invoice.Cnpj, invoice.Modelo, invoice.Serie, invoice.Numero);
        var items = doc.Reg54
            .Where(i => NormKey(i.Cnpj, i.Modelo, i.Serie, i.Numero) == targetKey)
            .OrderBy(i => i.NumItem, StringComparer.Ordinal)
            .ToList();

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(18)
        };

        // Top Summary Card
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

        var topTitleGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        string direction = TaxIdValidator.IsEntradaCfop(invoice.Cfop) ? "Entrada (Compra / Aquisição)" : "Saída (Emissão Própria)";
        topTitleGrid.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = $"NF {numDisplay}  |  Modelo {invoice.Modelo}  |  Série '{invoice.Serie.Trim()}'",
                    FontSize = 16,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush.Parse("#0F172A")
                },
                new TextBlock
                {
                    Text = $"{direction}  -  CFOP {invoice.Cfop} ({TaxIdValidator.CfopNature(invoice.Cfop)})",
                    FontSize = 12,
                    Foreground = Brush.Parse("#475569")
                }
            }
        });

        bool cancelled = invoice.Situacao == "S";
        var statusPill = new Border
        {
            Background = Brush.Parse(cancelled ? "#FEE2E2" : "#DCFCE7"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = cancelled ? "Cancelada (Sit. S)" : "Normal (Sit. N)",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse(cancelled ? "#B91C1C" : "#15803D")
            }
        };
        Grid.SetColumn(statusPill, 1);
        topTitleGrid.Children.Add(statusPill);
        hStack.Children.Add(topTitleGrid);

        var infoGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto")
        };

        void AddKv(int col, int row, string k, string v, string? color = null)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 12, 4) };
            sp.Children.Add(new TextBlock { Text = k, FontSize = 11, Foreground = Brush.Parse("#475569") });
            sp.Children.Add(new TextBlock { Text = v, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse(color ?? "#0F172A") });
            Grid.SetColumn(sp, col);
            Grid.SetRow(sp, row);
            infoGrid.Children.Add(sp);
        }

        AddKv(0, 0, "Data Emissão / Entrada", TaxIdValidator.FormatDateBr(invoice.DataEmissao));
        AddKv(1, 0, "CNPJ Emit./Dest.", TaxIdValidator.FormatCnpj(invoice.Cnpj));
        AddKv(2, 0, "Inscrição Estadual / UF", $"{invoice.Ie.Trim()} ({invoice.Uf})");
        AddKv(3, 0, "Emitente (P/T)", invoice.Emitente == "P" ? "Próprio (P)" : "Terceiros (T)");

        AddKv(0, 1, "Valor Total da Nota", $"R$ {TaxIdValidator.FormatCentsBrl(invoice.ValorTotalCents)}", "#1D4ED8");
        AddKv(1, 1, "Base de Cálculo ICMS", $"R$ {TaxIdValidator.FormatCentsBrl(invoice.BaseIcmsCents)}");
        AddKv(2, 1, "Valor do ICMS", $"R$ {TaxIdValidator.FormatCentsBrl(invoice.ValorIcmsCents)}");
        AddKv(3, 1, "Isentas / Outras", $"R$ {TaxIdValidator.FormatCentsBrl(invoice.IsentaNaoTribCents)} / R$ {TaxIdValidator.FormatCentsBrl(invoice.OutrasCents)}");

        hStack.Children.Add(infoGrid);

        if (xmlMatch is not null)
        {
            hStack.Children.Add(new Border
            {
                Background = Brush.Parse("#EFF6FF"),
                BorderBrush = Brush.Parse("#BFDBFE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6),
                Child = new TextBlock
                {
                    Text = $"Conferência XML: {xmlMatch.MatchStatus} - Fornecedor: {xmlMatch.EmitNome} (Valor XML: R$ {TaxIdValidator.FormatCentsBrl(xmlMatch.XmlValorCents)})",
                    FontSize = 11.5,
                    Foreground = Brush.Parse("#1E3A8A")
                }
            });
        }

        headerCard.Child = hStack;
        Grid.SetRow(headerCard, 0);
        root.Children.Add(headerCard);

        // Middle: Reg 54 Items List
        var itemsCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12)
        };

        var itemsLayout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        itemsLayout.Children.Add(new TextBlock
        {
            Text = $"Produtos / Itens da Nota (Registro 54: {items.Count} item(ns))",
            FontSize = 13.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#0F172A"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var colLens = new[]
        {
            new GridLength(46), new GridLength(85), new GridLength(1.8, GridUnitType.Star),
            new GridLength(50), new GridLength(55), new GridLength(70),
            new GridLength(95), new GridLength(90)
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
        AddCell(hdr, 0, "Item", true, true, "#334155");
        AddCell(hdr, 1, "Código", false, true, "#334155");
        AddCell(hdr, 2, "Descrição (Registro 75)", false, true, "#334155");
        AddCell(hdr, 3, "CST", false, true, "#334155");
        AddCell(hdr, 4, "CFOP", false, true, "#334155");
        AddCell(hdr, 5, "Qtd.", true, true, "#334155");
        AddCell(hdr, 6, "Valor Bruto", true, true, "#334155");
        AddCell(hdr, 7, "Base ICMS", true, true, "#334155");

        var hdrBorder = new Border
        {
            Background = Brush.Parse("#F1F5F9"),
            BorderBrush = Brush.Parse("#E2E8F0"),
            BorderThickness = new Thickness(1, 1, 1, 0),
            Child = hdr
        };
        Grid.SetRow(hdrBorder, 1);
        itemsLayout.Children.Add(hdrBorder);

        if (items.Count == 0)
        {
            var empty = new Border
            {
                BorderBrush = Brush.Parse("#E2E8F0"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Child = new TextBlock
                {
                    Text = "Esta nota fiscal foi escriturada apenas pelo resumo do Registro 50 (sem itens Reg. 54 detalhados neste arquivo).",
                    FontSize = 12,
                    Foreground = Brush.Parse("#475569"),
                    TextWrapping = TextWrapping.Wrap
                }
            };
            Grid.SetRow(empty, 2);
            itemsLayout.Children.Add(empty);
        }
        else
        {
            var pad = new Thickness(6, 5);
            var lb = UiTableHelpers.CreateRecycledListBox<Reg54>(
                items,
                colLens,
                (g, slots, it) =>
                {
                    string desc = it.NumItem switch
                    {
                        "991" => "FRETE (ITEM ACESSÓRIO 991)",
                        "992" => "SEGURO (ITEM ACESSÓRIO 992)",
                        "999" => "OUTRAS DESPESAS / ST (ITEM 999)",
                        _ => r75Map.TryGetValue(it.CodProduto, out var r75) ? r75.Descricao : $"PRODUTO {it.CodProduto}"
                    };
                    long val = it.NumItem is "991" or "992" or "999" ? it.ValorDescontoCents : it.ValorProdutoCents;
                    UiTableHelpers.SetCellSlot(g, slots, 0, it.NumItem, TextAlignment.Right, FontWeight.SemiBold, Brush.Parse("#475569"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 1, it.CodProduto, TextAlignment.Left, FontWeight.Normal, Brush.Parse("#334155"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 2, desc, TextAlignment.Left, FontWeight.Normal, Brush.Parse("#0F172A"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 3, it.Cst, TextAlignment.Left, FontWeight.Normal, Brush.Parse("#475569"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 4, it.Cfop, TextAlignment.Left, FontWeight.SemiBold, Brush.Parse("#1D4ED8"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 5, TaxIdValidator.FormatMilliBr(it.QuantidadeMilli), TextAlignment.Right, FontWeight.Normal, Brush.Parse("#334155"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 6, $"R$ {TaxIdValidator.FormatCentsBrl(val)}", TextAlignment.Right, FontWeight.SemiBold, Brush.Parse("#0F172A"), 11.5, pad);
                    UiTableHelpers.SetCellSlot(g, slots, 7, $"R$ {TaxIdValidator.FormatCentsBrl(it.BaseIcmsCents)}", TextAlignment.Right, FontWeight.Normal, Brush.Parse("#475569"), 11.5, pad);
                },
                borderThickness: new Thickness(1),
                borderBrush: Brush.Parse("#E2E8F0"));
            UiTableHelpers.AttachSyncedHeaderPadding(lb, hdr);
            Grid.SetRow(lb, 2);
            itemsLayout.Children.Add(lb);
        }

        itemsCard.Child = itemsLayout;
        Grid.SetRow(itemsCard, 1);
        root.Children.Add(itemsCard);

        // Footer Buttons
        var foot = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 12, 0, 0)
        };

        var btnRaw = new Button
        {
            Content = $"Inspecionar colunas do Registro 50 (Linha {invoice.Line})",
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            Foreground = Brush.Parse("#334155"),
            Padding = new Thickness(12, 7),
            CornerRadius = new CornerRadius(5)
        };
        btnRaw.Click += async (_, _) =>
        {
            if (invoice.Line >= 1 && invoice.Line <= doc.Lines.Count)
            {
                var insp = new LineInspectorWindow(doc.Lines[invoice.Line - 1], 1, 126);
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
    }
}
