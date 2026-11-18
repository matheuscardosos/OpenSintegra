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
/// Dialog for launching omitted purchase XML NF-e documents or reprocessing already-loaded XMLs
/// into Sintegra (Reg 50 Entry Date &amp; CFOP, Reg 54/75 items, auto-save _ATUALIZADO.txt, and XML copies).
/// </summary>
public sealed class InjectMissingXmlWindow : Window
{
    public IReadOnlyList<XmlInjectionRequest>? ConfirmedRequests { get; private set; }
    public bool SimplesZeroIcms { get; private set; }
    public bool IncludeItems54 { get; private set; }
    public bool AutoSaveUpdatedSintegra { get; private set; }
    public bool ExportAdjustedXmlCopies { get; private set; }

    public InjectMissingXmlWindow(
        IReadOnlyList<XmlDocumentSummary> targets,
        string buyerUf,
        string companyTaxRegime,
        bool hasXmlSource = false)
        : this(
            targets.Select(x => new XmlPurchaseProcessCandidate(x, AlreadyInSintegra: false)).ToList(),
            buyerUf,
            companyTaxRegime,
            isReprocessMode: false,
            hasXmlSource: hasXmlSource)
    {
    }

    public InjectMissingXmlWindow(
        IReadOnlyList<XmlPurchaseProcessCandidate> items,
        string buyerUf,
        string companyTaxRegime,
        bool isReprocessMode,
        bool hasXmlSource)
    {
        Title = isReprocessMode
            ? $"Reprocessar XMLs no Sintegra ({items.Count})"
            : $"Lançar Notas Faltantes no Sintegra ({items.Count})";
        Icon = VectorIcons.AppWindowIcon;
        Width = 980;
        Height = 650;
        MinWidth = 780;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#EEF2F6");

        bool defaultZeroIcms = companyTaxRegime.Contains("Simples", StringComparison.OrdinalIgnoreCase) ||
                               companyTaxRegime.Contains("MEI", StringComparison.OrdinalIgnoreCase);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"),
            Margin = new Thickness(18)
        };

        int omissasCount = items.Count(i => !i.AlreadyInSintegra);
        int lancadasCount = items.Count(i => i.AlreadyInSintegra);
        long totalOmissasVal = items.Where(i => !i.AlreadyInSintegra).Sum(i => i.Xml.ValorTotalCents);

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
                        Text = isReprocessMode
                            ? $"Reprocessar XMLs no Sintegra ({lancadasCount} já lançada(s), {omissasCount} faltante(s))"
                            : $"Lançar {items.Count} Nota(s) Fiscal(is) de Compra diretamente no Sintegra",
                        FontSize = 15,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brush.Parse("#0F172A")
                    },
                    new TextBlock
                    {
                        Text = isReprocessMode
                            ? "Ajuste a Data de Entrada e/ou CFOP no Registro 50 sem precisar baixar os XMLs novamente. Notas faltantes marcadas serão incluídas e o Registro 90 será recalculado."
                            : $"Valor a acrescentar nas Entradas: R$ {TaxIdValidator.FormatCentsBrl(totalOmissasVal)}. Defina a Data de Entrada e o CFOP de cada nota; o Registro 90 será recalculado automaticamente.",
                        FontSize = 12,
                        Foreground = Brush.Parse("#475569"),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
        Grid.SetRow(banner, 0);
        root.Children.Add(banner);

        var chkZeroIcms = new CheckBox
        {
            Content = $"Zerar Base e Valor de ICMS nas novas entradas (padrão {companyTaxRegime})",
            IsChecked = defaultZeroIcms,
            FontSize = 12
        };
        var chkInclude54 = new CheckBox
        {
            Content = "Gerar itens e produtos detalhados das notas faltantes (Registros 54 e 75)",
            IsChecked = true,
            FontSize = 12
        };
        var chkAutoSave = new CheckBox
        {
            Content = "Salvar Sintegra automaticamente como arquivo \"_ATUALIZADO.txt\" na mesma pasta (sem pedir Salvar Como)",
            IsChecked = true,
            FontSize = 12
        };
        var chkExportXmlCopies = new CheckBox
        {
            Content = "Gerar cópias dos XMLs com dhSaiEnt ajustado na subpasta \"XMLs_Ajustados\" (preserva os originais intactos)",
            IsChecked = isReprocessMode && hasXmlSource,
            IsEnabled = hasXmlSource,
            FontSize = 12
        };
        if (!hasXmlSource)
        {
            chkExportXmlCopies.Content = "Gerar cópias ajustadas dos XMLs em subpasta separada (recarregue um ZIP ou Pasta de XMLs para habilitar)";
        }

        var optsPanel = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel
            {
                Spacing = 5,
                Children = { chkZeroIcms, chkInclude54, chkAutoSave, chkExportXmlCopies }
            }
        };
        Grid.SetRow(optsPanel, 1);
        root.Children.Add(optsPanel);

        var rowControls = new List<(XmlPurchaseProcessCandidate Item, CheckBox CheckBox, TextBox DateBox, TextBox CfopBox)>();

        // Batch date & selection toolbar
        var batchBar = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var batchWrap = new WrapPanel { Orientation = Orientation.Horizontal };

        var btnAll = MakeMiniButton("Marcar Todas");
        var btnMissing = MakeMiniButton("Só Faltantes");
        var btnNone = MakeMiniButton("Desmarcar");

        var txtBatchDate = new TextBox
        {
            Watermark = "DD/MM/AAAA",
            Width = 108,
            FontSize = 11.5,
            Padding = new Thickness(7, 4),
            Margin = new Thickness(12, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var btnApplyDate = MakeMiniButton("Aplicar Data às Marcadas", "#2563EB", Brushes.White);
        var btnPullSaiEnt = MakeMiniButton("Puxar dhSaiEnt do XML");
        var btnPullEmissao = MakeMiniButton("Puxar Emissão XML");

        btnAll.Margin = new Thickness(0, 2, 6, 2);
        btnMissing.Margin = new Thickness(0, 2, 6, 2);
        btnNone.Margin = new Thickness(0, 2, 6, 2);
        btnApplyDate.Margin = new Thickness(0, 2, 6, 2);
        btnPullSaiEnt.Margin = new Thickness(0, 2, 6, 2);
        btnPullEmissao.Margin = new Thickness(0, 2, 0, 2);

        batchWrap.Children.Add(btnAll);
        if (isReprocessMode)
            batchWrap.Children.Add(btnMissing);
        batchWrap.Children.Add(btnNone);
        batchWrap.Children.Add(txtBatchDate);
        batchWrap.Children.Add(btnApplyDate);
        batchWrap.Children.Add(btnPullSaiEnt);
        batchWrap.Children.Add(btnPullEmissao);
        batchBar.Child = batchWrap;
        Grid.SetRow(batchBar, 2);
        root.Children.Add(batchBar);

        // Invoices list
        var listCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10)
        };
        var listStack = new StackPanel { Spacing = 6 };
        string colSpec = "32,96,112,118,*,104,88";
        var hdr = new Grid { ColumnDefinitions = new ColumnDefinitions(colSpec), Background = Brush.Parse("#F1F5F9") };
        void AddHdr(int c, string t)
        {
            var tb = new TextBlock
            {
                Text = t,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#334155"),
                Padding = new Thickness(6, 5)
            };
            Grid.SetColumn(tb, c);
            hdr.Children.Add(tb);
        }
        AddHdr(0, "");
        AddHdr(1, "Emissão XML");
        AddHdr(2, "No Sintegra");
        AddHdr(3, "Data Entrada");
        AddHdr(4, "NF / Fornecedor");
        AddHdr(5, "Valor Total");
        AddHdr(6, "CFOP Entr.");
        listStack.Children.Add(hdr);

        var scroll = new ScrollViewer { MaxHeight = 295 };
        var rowsPanel = new StackPanel();
        foreach (var item in items)
        {
            var xml = item.Xml;
            string suggestedCfop = item.AlreadyInSintegra && !string.IsNullOrWhiteSpace(item.ExistingSintegraCfop)
                ? item.ExistingSintegraCfop
                : SintegraXmlInjector.SuggestEntryCfop(xml.Cfops.FirstOrDefault(), xml.FullData?.SupplierUf, buyerUf);

            string initialEntryDateYmd = !string.IsNullOrWhiteSpace(item.ExistingSintegraDateYmd) &&
                                         TaxIdValidator.ValidateDateYyyyMmDd(item.ExistingSintegraDateYmd)
                ? item.ExistingSintegraDateYmd
                : xml.DataEmissao;

            string initialDateBr = TaxIdValidator.FormatDateBr(initialEntryDateYmd);

            var rowGrid = new Grid { ColumnDefinitions = new ColumnDefinitions(colSpec), Margin = new Thickness(0, 2) };

            var chk = new CheckBox
            {
                IsChecked = !isReprocessMode || !item.AlreadyInSintegra,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var emiStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 2) };
            emiStack.Children.Add(new TextBlock
            {
                Text = TaxIdValidator.FormatDateBr(xml.DataEmissao),
                FontSize = 11.5,
                Foreground = Brush.Parse("#0F172A")
            });
            if (!string.IsNullOrWhiteSpace(xml.DataSaiEnt) && xml.DataSaiEnt != xml.DataEmissao)
            {
                emiStack.Children.Add(new TextBlock
                {
                    Text = $"Saída: {TaxIdValidator.FormatDateBr(xml.DataSaiEnt)}",
                    FontSize = 10,
                    Foreground = Brush.Parse("#0284C7")
                });
            }

            var tStatus = new TextBlock
            {
                Text = item.AlreadyInSintegra
                    ? $"Lanç. {TaxIdValidator.FormatDateBr(item.ExistingSintegraDateYmd ?? "")}"
                    : "Omissa",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse(item.AlreadyInSintegra ? "#15803D" : "#DC2626"),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 4)
            };

            var txtEntryDate = new TextBox
            {
                Text = initialDateBr,
                MaxLength = 10,
                Width = 104,
                FontSize = 11.5,
                Padding = new Thickness(6, 3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var nfText = new TextBlock
            {
                Text = $"NF {xml.Numero.TrimStart('0').PadLeft(1, '0')}/{xml.Serie} - {xml.EmitNome}",
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Padding = new Thickness(6, 4)
            };

            var tVal = new TextBlock
            {
                Text = $"R$ {TaxIdValidator.FormatCentsBrl(xml.ValorTotalCents)}",
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                Padding = new Thickness(6, 4)
            };

            var txtCfop = new TextBox
            {
                Text = item.HasMultipleCfopsInSintegra ? "VÁRIOS" : suggestedCfop,
                IsReadOnly = item.HasMultipleCfopsInSintegra,
                MaxLength = 6,
                Width = 74,
                FontSize = 11.5,
                Padding = new Thickness(6, 3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(chk, 0);
            Grid.SetColumn(emiStack, 1);
            Grid.SetColumn(tStatus, 2);
            Grid.SetColumn(txtEntryDate, 3);
            Grid.SetColumn(nfText, 4);
            Grid.SetColumn(tVal, 5);
            Grid.SetColumn(txtCfop, 6);

            rowGrid.Children.Add(chk);
            rowGrid.Children.Add(emiStack);
            rowGrid.Children.Add(tStatus);
            rowGrid.Children.Add(txtEntryDate);
            rowGrid.Children.Add(nfText);
            rowGrid.Children.Add(tVal);
            rowGrid.Children.Add(txtCfop);

            rowsPanel.Children.Add(new Border
            {
                BorderBrush = Brush.Parse("#E2E8F0"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = rowGrid
            });
            rowControls.Add((item, chk, txtEntryDate, txtCfop));
        }
        scroll.Content = rowsPanel;
        listStack.Children.Add(scroll);
        listCard.Child = listStack;
        Grid.SetRow(listCard, 3);
        root.Children.Add(listCard);

        var lblValidation = new TextBlock
        {
            FontSize = 11.5,
            Foreground = Brush.Parse("#DC2626"),
            VerticalAlignment = VerticalAlignment.Center
        };

        btnAll.Click += (_, _) =>
        {
            foreach (var r in rowControls) r.CheckBox.IsChecked = true;
        };
        btnMissing.Click += (_, _) =>
        {
            foreach (var r in rowControls) r.CheckBox.IsChecked = !r.Item.AlreadyInSintegra;
        };
        btnNone.Click += (_, _) =>
        {
            foreach (var r in rowControls) r.CheckBox.IsChecked = false;
        };
        btnApplyDate.Click += (_, _) =>
        {
            string? parsed = TaxIdValidator.ParseFlexibleDateYyyyMmDd(txtBatchDate.Text);
            if (parsed is null)
            {
                lblValidation.Text = "Informe uma data válida no campo em lote (DD/MM/AAAA).";
                return;
            }
            lblValidation.Text = "";
            string br = TaxIdValidator.FormatDateBr(parsed);
            foreach (var r in rowControls)
            {
                if (r.CheckBox.IsChecked == true)
                    r.DateBox.Text = br;
            }
        };
        btnPullSaiEnt.Click += (_, _) =>
        {
            lblValidation.Text = "";
            foreach (var r in rowControls)
            {
                if (r.CheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(r.Item.Xml.DataSaiEnt))
                    r.DateBox.Text = TaxIdValidator.FormatDateBr(r.Item.Xml.DataSaiEnt);
            }
        };
        btnPullEmissao.Click += (_, _) =>
        {
            lblValidation.Text = "";
            foreach (var r in rowControls)
            {
                if (r.CheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(r.Item.Xml.DataEmissao))
                    r.DateBox.Text = TaxIdValidator.FormatDateBr(r.Item.Xml.DataEmissao);
            }
        };

        var bottomGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetColumn(lblValidation, 0);
        bottomGrid.Children.Add(lblValidation);

        var btnBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
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
            Content = isReprocessMode ? "Aplicar Reprocessamento no Sintegra" : "Confirmar Lançamento no Sintegra",
            Background = Brush.Parse("#16A34A"),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(18, 7),
            CornerRadius = new CornerRadius(5)
        };
        btnConfirm.Click += (_, _) =>
        {
            var confirmed = new List<XmlInjectionRequest>();
            foreach (var r in rowControls)
            {
                if (r.CheckBox.IsChecked != true) continue;

                string? entryYmd = TaxIdValidator.ParseFlexibleDateYyyyMmDd(r.DateBox.Text);
                if (entryYmd is null)
                {
                    lblValidation.Text = $"Data de Entrada inválida na NF {r.Item.Xml.Numero.TrimStart('0')}. Use DD/MM/AAAA.";
                    r.DateBox.Focus();
                    return;
                }

                string cfop = r.Item.HasMultipleCfopsInSintegra
                    ? ""
                    : (r.CfopBox.Text ?? "1102").Trim();

                if (!r.Item.HasMultipleCfopsInSintegra && !TaxIdValidator.ValidateCfop(cfop))
                {
                    lblValidation.Text = $"CFOP de Entrada inválido na NF {r.Item.Xml.Numero.TrimStart('0')}. Use 4 dígitos (ex.: 1102).";
                    r.CfopBox.Focus();
                    return;
                }

                confirmed.Add(new XmlInjectionRequest(r.Item.Xml, cfop, entryYmd));
            }

            if (confirmed.Count == 0)
            {
                lblValidation.Text = "Marque ao menos uma nota fiscal para aplicar.";
                return;
            }

            ConfirmedRequests = confirmed;
            SimplesZeroIcms = chkZeroIcms.IsChecked ?? defaultZeroIcms;
            IncludeItems54 = chkInclude54.IsChecked ?? true;
            AutoSaveUpdatedSintegra = chkAutoSave.IsChecked ?? true;
            ExportAdjustedXmlCopies = hasXmlSource && (chkExportXmlCopies.IsChecked ?? false);
            Close(true);
        };

        btnBar.Children.Add(btnCancel);
        btnBar.Children.Add(btnConfirm);
        Grid.SetColumn(btnBar, 1);
        bottomGrid.Children.Add(btnBar);
        Grid.SetRow(bottomGrid, 4);
        root.Children.Add(bottomGrid);

        Content = root;
    }

    private static Button MakeMiniButton(string text, string bgHex = "#F1F5F9", IBrush? fg = null) => new()
    {
        Content = text,
        FontSize = 11.5,
        FontWeight = FontWeight.SemiBold,
        Background = Brush.Parse(bgHex),
        Foreground = fg ?? Brush.Parse("#334155"),
        BorderBrush = Brush.Parse("#CBD5E1"),
        BorderThickness = new Thickness(bgHex == "#F1F5F9" ? 1 : 0),
        Padding = new Thickness(10, 4),
        CornerRadius = new CornerRadius(4)
    };
}
