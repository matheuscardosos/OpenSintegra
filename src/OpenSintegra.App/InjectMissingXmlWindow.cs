using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OpenSintegra.Core;

namespace OpenSintegra.App;

/// <summary>
/// Confirmation dialog allowing the accountant to launch omitted purchase XML NF-e documents
/// directly into the loaded Sintegra file (Reg 50, Reg 54, Reg 75 + recalculated Reg 90).
/// </summary>
public sealed class InjectMissingXmlWindow : Window
{
    public IReadOnlyList<XmlInjectionRequest>? ConfirmedRequests { get; private set; }
    public bool SimplesZeroIcms { get; private set; }
    public bool IncludeItems54 { get; private set; }

    public InjectMissingXmlWindow(
        IReadOnlyList<XmlDocumentSummary> targets,
        string buyerUf,
        string companyTaxRegime)
    {
        Title = $"Lançar Notas Faltantes no Sintegra ({targets.Count})";
        Icon = VectorIcons.AppWindowIcon;
        Width = 820;
        Height = 560;
        MinWidth = 680;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#EEF2F6");

        bool defaultZeroIcms = companyTaxRegime.Contains("Simples", StringComparison.OrdinalIgnoreCase) ||
                               companyTaxRegime.Contains("MEI", StringComparison.OrdinalIgnoreCase);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            Margin = new Thickness(18)
        };

        // Top info banner
        long totalVal = targets.Sum(t => t.ValorTotalCents);
        var banner = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Lançar {targets.Count} Nota(s) Fiscal(is) de Compra diretamente no Sintegra",
                        FontSize = 15,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brush.Parse("#0F172A")
                    },
                    new TextBlock
                    {
                        Text = $"Valor total a acrescentar nas Entradas: R$ {TaxIdValidator.FormatCentsBrl(totalVal)}. O Registro 90 será recalculado automaticamente.",
                        FontSize = 12,
                        Foreground = Brush.Parse("#475569"),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
        Grid.SetRow(banner, 0);
        root.Children.Add(banner);

        // Options Checkboxes
        var chkZeroIcms = new CheckBox
        {
            Content = $"Zerar Base e Valor de ICMS nas entradas (padrão {companyTaxRegime})",
            IsChecked = defaultZeroIcms,
            FontSize = 12
        };
        var chkInclude54 = new CheckBox
        {
            Content = "Gerar itens e produtos detalhados do XML (Registros 54 e 75)",
            IsChecked = true,
            FontSize = 12
        };
        var optsPanel = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel { Spacing = 6, Children = { chkZeroIcms, chkInclude54 } }
        };
        Grid.SetRow(optsPanel, 1);
        root.Children.Add(optsPanel);

        // Invoices list with editable Entry CFOP
        var cfopInputs = new List<(XmlDocumentSummary Xml, TextBox CfopBox)>();
        var listCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10)
        };
        var listStack = new StackPanel { Spacing = 6 };
        var hdr = new Grid { ColumnDefinitions = new ColumnDefinitions("80,80,*,110,95"), Background = Brush.Parse("#F1F5F9") };
        void AddHdr(int c, string t)
        {
            var tb = new TextBlock { Text = t, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#334155"), Padding = new Thickness(6, 5) };
            Grid.SetColumn(tb, c);
            hdr.Children.Add(tb);
        }
        AddHdr(0, "Emissão");
        AddHdr(1, "Nota");
        AddHdr(2, "Fornecedor");
        AddHdr(3, "Valor Total");
        AddHdr(4, "CFOP Entrada");
        listStack.Children.Add(hdr);

        var scroll = new ScrollViewer { MaxHeight = 250 };
        var rowsPanel = new StackPanel();
        foreach (var xml in targets)
        {
            string suggestedCfop = SintegraXmlInjector.SuggestEntryCfop(xml.Cfops.FirstOrDefault(), xml.FullData?.SupplierUf, buyerUf);
            var rowGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("80,80,*,110,95"), Margin = new Thickness(0, 2) };

            var tDate = new TextBlock { Text = TaxIdValidator.FormatDateBr(xml.DataEmissao), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 4) };
            var tNum = new TextBlock { Text = $"{xml.Numero}/{xml.Serie}", FontSize = 11.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 4) };
            var tEmit = new TextBlock { Text = xml.EmitNome, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Padding = new Thickness(6, 4) };
            var tVal = new TextBlock { Text = $"R$ {TaxIdValidator.FormatCentsBrl(xml.ValorTotalCents)}", FontSize = 11.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, Padding = new Thickness(6, 4) };
            var txtCfop = new TextBox { Text = suggestedCfop, MaxLength = 4, Width = 78, FontSize = 11.5, Padding = new Thickness(6, 3), HorizontalAlignment = HorizontalAlignment.Center };

            Grid.SetColumn(tDate, 0);
            Grid.SetColumn(tNum, 1);
            Grid.SetColumn(tEmit, 2);
            Grid.SetColumn(tVal, 3);
            Grid.SetColumn(txtCfop, 4);
            rowGrid.Children.Add(tDate);
            rowGrid.Children.Add(tNum);
            rowGrid.Children.Add(tEmit);
            rowGrid.Children.Add(tVal);
            rowGrid.Children.Add(txtCfop);

            rowsPanel.Children.Add(new Border
            {
                BorderBrush = Brush.Parse("#E2E8F0"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = rowGrid
            });
            cfopInputs.Add((xml, txtCfop));
        }
        scroll.Content = rowsPanel;
        listStack.Children.Add(scroll);
        listCard.Child = listStack;
        Grid.SetRow(listCard, 2);
        root.Children.Add(listCard);

        // Action Buttons
        var btnBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var btnCancel = new Button
        {
            Content = "Cancelar",
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            Foreground = Brush.Parse("#334155"),
            Padding = new Thickness(16, 7),
            CornerRadius = new CornerRadius(5)
        };
        btnCancel.Click += (_, _) => Close(false);

        var btnConfirm = new Button
        {
            Content = "Confirmar Lançamento no Sintegra",
            Background = Brush.Parse("#16A34A"),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(18, 7),
            CornerRadius = new CornerRadius(5)
        };
        btnConfirm.Click += (_, _) =>
        {
            ConfirmedRequests = cfopInputs
                .Select(pair => new XmlInjectionRequest(pair.Xml, (pair.CfopBox.Text ?? "1102").Trim()))
                .ToList();
            SimplesZeroIcms = chkZeroIcms.IsChecked ?? defaultZeroIcms;
            IncludeItems54 = chkInclude54.IsChecked ?? true;
            Close(true);
        };

        btnBar.Children.Add(btnCancel);
        btnBar.Children.Add(btnConfirm);
        Grid.SetRow(btnBar, 3);
        root.Children.Add(btnBar);

        Content = root;
    }
}
