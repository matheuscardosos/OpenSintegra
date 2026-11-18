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
/// Shows full details of an audited/missing XML invoice (access key, supplier, taxes, Sintegra status,
/// and all Reg/XML items), with a direct action to launch omitted purchase invoices into Sintegra.
/// </summary>
public sealed class XmlInvoiceDetailsWindow : Window
{
    public bool LaunchRequested { get; private set; }

    public XmlInvoiceDetailsWindow(
        XmlDocumentSummary xml,
        XmlAuditDocumentRow auditRow,
        bool canLaunch)
    {
        string numDisplay = xml.Numero.TrimStart('0').PadLeft(1, '0');
        string serDisplay = string.IsNullOrWhiteSpace(xml.Serie) ? "0" : xml.Serie.Trim();

        Title = $"Detalhes da Nota XML {numDisplay} (Série {serDisplay})";
        Icon = VectorIcons.AppWindowIcon;
        Width = 920;
        Height = 650;
        MinWidth = 740;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#EEF2F6");

        var items = xml.FullData?.Items ?? Array.Empty<XmlProductItem>();

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(18)
        };

        // Top Header Card
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
        topTitleGrid.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = $"NF-e {numDisplay}  |  Modelo {xml.Modelo}  |  Série '{serDisplay}'",
                    FontSize = 16,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush.Parse("#0F172A")
                },
                new TextBlock
                {
                    Text = $"{auditRow.CategoryLabel}  -  Natureza XML: {(string.IsNullOrWhiteSpace(xml.NatOp) ? "-" : xml.NatOp)}",
                    FontSize = 12,
                    Foreground = Brush.Parse("#475569"),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        });

        bool isGap = auditRow.IsCriticalGap;
        bool isCancelled = xml.Cancelled;
        var statusPill = new Border
        {
            Background = Brush.Parse(isGap ? "#FEE2E2" : (isCancelled ? "#FEF3C7" : "#DCFCE7")),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = auditRow.MatchStatus,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse(isGap ? "#B91C1C" : (isCancelled ? "#B45309" : "#15803D"))
            }
        };
        Grid.SetColumn(statusPill, 1);
        topTitleGrid.Children.Add(statusPill);
        hStack.Children.Add(topTitleGrid);

        var infoGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.4*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto")
        };

        void AddKv(int col, int row, string k, string v, string? color = null)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 3, 12, 3) };
            sp.Children.Add(new TextBlock { Text = k, FontSize = 11, Foreground = Brush.Parse("#475569") });
            sp.Children.Add(new TextBlock
            {
                Text = v,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse(color ?? "#0F172A"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(sp, col);
            Grid.SetRow(sp, row);
            infoGrid.Children.Add(sp);
        }

        string ufStr = string.IsNullOrWhiteSpace(xml.FullData?.SupplierUf) ? "-" : xml.FullData.SupplierUf;
        string ieStr = string.IsNullOrWhiteSpace(xml.FullData?.SupplierIe) ? "-" : xml.FullData.SupplierIe;
        AddKv(0, 0, "Fornecedor / Emitente", string.IsNullOrWhiteSpace(xml.EmitNome) ? "-" : xml.EmitNome);
        AddKv(1, 0, "CNPJ / CPF", TaxIdValidator.FormatCnpj(xml.EmitCnpj));
        AddKv(2, 0, "IE / UF Emitente", $"{ieStr} ({ufStr})");
        string dtText = !string.IsNullOrWhiteSpace(xml.DataSaiEnt) && xml.DataSaiEnt != xml.DataEmissao
            ? $"{TaxIdValidator.FormatDateBr(xml.DataEmissao)} (Saída: {TaxIdValidator.FormatDateBr(xml.DataSaiEnt)})"
            : TaxIdValidator.FormatDateBr(xml.DataEmissao);
        AddKv(3, 0, "Emissão / Saída XML", dtText);

        long frete = xml.FullData?.FreteCents ?? 0;
        long st = xml.FullData?.IcmsStCents ?? 0;
        long outras = xml.FullData?.OutrasDespCents ?? 0;
        AddKv(0, 1, "Valor Total da Nota (XML)", $"R$ {TaxIdValidator.FormatCentsBrl(xml.ValorTotalCents)}", isGap ? "#B91C1C" : "#1D4ED8");
        AddKv(1, 1, "Base / Valor ICMS", $"R$ {TaxIdValidator.FormatCentsBrl(xml.BaseIcmsCents)} / R$ {TaxIdValidator.FormatCentsBrl(xml.ValorIcmsCents)}");
        AddKv(2, 1, "Frete / ST / Outras", $"R$ {TaxIdValidator.FormatCentsBrl(frete + st + outras)}");
        AddKv(3, 1, "CFOP(s) Origem XML", string.IsNullOrWhiteSpace(auditRow.XmlCfops) ? "-" : auditRow.XmlCfops);

        hStack.Children.Add(infoGrid);

        // Copyable 44-digit NF-e Access Key + File Name
        if (!string.IsNullOrWhiteSpace(xml.Chave) || !string.IsNullOrWhiteSpace(xml.FileName))
        {
            var keyGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            keyGrid.Children.Add(new TextBlock
            {
                Text = "Chave de Acesso:",
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#475569"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            var txtChave = new TextBox
            {
                Text = string.IsNullOrWhiteSpace(xml.Chave) ? "-" : xml.Chave,
                IsReadOnly = true,
                FontSize = 11.5,
                Foreground = Brush.Parse("#0F172A"),
                Background = Brush.Parse("#F8FAFC"),
                BorderBrush = Brush.Parse("#E2E8F0"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4),
                CornerRadius = new CornerRadius(4)
            };
            Grid.SetColumn(txtChave, 1);
            keyGrid.Children.Add(txtChave);

            var lblFile = new TextBlock
            {
                Text = xml.FileName,
                FontSize = 11,
                Foreground = Brush.Parse("#475569"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                MaxWidth = 220,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(lblFile, 2);
            keyGrid.Children.Add(lblFile);
            hStack.Children.Add(keyGrid);
        }

        string statusExplain = isGap
            ? "Não localizada em nenhum mês do arquivo Sintegra carregado (busca realizada em todo o período)."
            : (string.IsNullOrWhiteSpace(auditRow.Notes)
                ? $"Localizada no Sintegra (CFOP: {auditRow.SintegraCfops} | Valor: R$ {TaxIdValidator.FormatCentsBrl(auditRow.SintegraValorCents)})."
                : auditRow.Notes);

        hStack.Children.Add(new Border
        {
            Background = Brush.Parse(isGap ? "#FEF2F2" : "#EFF6FF"),
            BorderBrush = Brush.Parse(isGap ? "#FECACA" : "#BFDBFE"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6),
            Child = new TextBlock
            {
                Text = $"Conferência no Sintegra: {statusExplain}",
                FontSize = 11.5,
                Foreground = Brush.Parse(isGap ? "#991B1B" : "#1E3A8A"),
                TextWrapping = TextWrapping.Wrap
            }
        });

        headerCard.Child = hStack;
        Grid.SetRow(headerCard, 0);
        root.Children.Add(headerCard);

        // Middle: XML Items Table
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
            Text = $"Produtos / Itens da Nota XML ({items.Count} item(ns))",
            FontSize = 13.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#0F172A"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var colLens = new[]
        {
            new GridLength(40), new GridLength(82), new GridLength(1.9, GridUnitType.Star),
            new GridLength(70), new GridLength(50), new GridLength(46),
            new GridLength(44), new GridLength(66), new GridLength(95), new GridLength(54)
        };

        Grid MakeGrid()
        {
            var g = new Grid();
            foreach (var c in colLens) g.ColumnDefinitions.Add(new ColumnDefinition(c));
            return g;
        }

        void AddCell(Grid g, int col, string text, bool right, bool bold, string hex)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
                Foreground = Brush.Parse(hex),
                TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
                Padding = new Thickness(5, 5),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }

        var hdr = MakeGrid();
        hdr.Margin = new Thickness(0, 0, 14, 0);
        AddCell(hdr, 0, "Item", true, true, "#334155");
        AddCell(hdr, 1, "Código", false, true, "#334155");
        AddCell(hdr, 2, "Descrição do Produto", false, true, "#334155");
        AddCell(hdr, 3, "NCM", false, true, "#334155");
        AddCell(hdr, 4, "CFOP", false, true, "#334155");
        AddCell(hdr, 5, "CST", false, true, "#334155");
        AddCell(hdr, 6, "Un.", false, true, "#334155");
        AddCell(hdr, 7, "Qtd.", true, true, "#334155");
        AddCell(hdr, 8, "Valor Bruto", true, true, "#334155");
        AddCell(hdr, 9, "Alíq.", true, true, "#334155");

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
                    Text = "Nenhum item de mercadoria (<det>) detalhado neste XML.",
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
            var lb = new ListBox
            {
                ItemsSource = items,
                Background = Brushes.White,
                BorderBrush = Brush.Parse("#E2E8F0"),
                BorderThickness = new Thickness(1),
                ItemTemplate = new FuncDataTemplate<XmlProductItem>((it, _) =>
                {
                    if (it is null) return new Panel();
                    var g = MakeGrid();
                    AddCell(g, 0, it.NumItem, true, true, "#475569");
                    AddCell(g, 1, it.CodProduto, false, false, "#334155");
                    AddCell(g, 2, it.Descricao, false, false, "#0F172A");
                    AddCell(g, 3, it.Ncm, false, false, "#475569");
                    AddCell(g, 4, it.OrigCfop, false, true, "#1D4ED8");
                    AddCell(g, 5, it.CstCsosn, false, false, "#475569");
                    AddCell(g, 6, it.Unidade, false, false, "#475569");
                    AddCell(g, 7, TaxIdValidator.FormatMilliBr(it.QuantidadeMilli), true, false, "#334155");
                    AddCell(g, 8, $"R$ {TaxIdValidator.FormatCentsBrl(it.ValorBrutoCents)}", true, true, "#0F172A");
                    AddCell(g, 9, $"{it.AliquotaIcmsBps / 100.0:N1}%", true, false, "#475569");
                    return new Border
                    {
                        BorderBrush = Brush.Parse("#E2E8F0"),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Child = g
                    };
                }, supportsRecycling: false)
            };
            lb.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                    new Setter(MarginProperty, new Thickness(0)),
                    new Setter(MinHeightProperty, 0.0),
                    new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)
                }
            });
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

        if (canLaunch && isGap)
        {
            var btnLaunch = new Button
            {
                Content = "Lançar esta NF no Sintegra...",
                FontWeight = FontWeight.SemiBold,
                Padding = new Thickness(16, 7)
            };
            btnLaunch.Classes.Add("success");
            btnLaunch.Click += (_, _) => { LaunchRequested = true; Close(); };
            Grid.SetColumn(btnLaunch, 0);
            foot.Children.Add(btnLaunch);
        }

        var btnClose = new Button
        {
            Content = "Fechar",
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(20, 7)
        };
        btnClose.Classes.Add("primary");
        btnClose.Click += (_, _) => Close();
        Grid.SetColumn(btnClose, 2);
        foot.Children.Add(btnClose);

        Grid.SetRow(foot, 2);
        root.Children.Add(foot);

        Content = root;
    }
}
