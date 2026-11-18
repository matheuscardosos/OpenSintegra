using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using OpenSintegra.Core;
using OpenSintegra.Data;
using OpenSintegra.Reports;

namespace OpenSintegra.App;

public sealed class MainWindow : Window
{
    private const string AppVersion = "1.0.0";

    private enum NavSection
    {
        VisaoGeral,
        NotasFaltantes,
        NotasFiscais,
        CfopEProdutos,
        CorrecaoEExportacao,
        EmpresasEBanco
    }

    // Core state & services
    private readonly FiscalDatabase _db = new();
    private bool _hasDocument;
    private bool _hasUnsavedChanges;
    private string _currentFilePath = "";
    private string _currentFileName = "Nenhum arquivo aberto";
    private byte[] _currentBytes = Array.Empty<byte>();
    private SintegraDocument _doc = new();
    private ValidationReport _report = null!;
    private IReadOnlyList<XmlDocumentSummary> _loadedXmlDocs = Array.Empty<XmlDocumentSummary>();
    private List<string> _loadedXmlSourcePaths = new();
    private SpedParsedData? _loadedSped;
    private AuditReport? _audit;
    private string? _lastXmlSourceLabel;
    private string _selectedYearMonth = "ALL";
    private ScopedFiscalView _scoped = null!;
    private NavSection _activeSection = NavSection.VisaoGeral;
    private FixReport? _lastFixReport;

    // UI filters
    private string _docSearchFilter = "";
    private string _docRegFilter = "ENT"; // "ENT" = Entradas Reg 50, "SAI50" = Saidas NF-e Reg 50, "61" = Cupons NFC-e Reg 61
    private string _auditFilter = "MISSING"; // Default: spotlight missing XML invoices!
    private string _diagFilter = "ALL";
    private string? _selectedCompanyCnpj;
    private bool _isCompactLayout;
    private bool IsCompactWidth => Bounds.Width > 100 && Bounds.Width < 1180;

    // Header & status controls
    private readonly TextBlock _hdrCompanyName = new();
    private readonly TextBlock _hdrCompanyDocs = new();
    private readonly ComboBox _cmbPeriod = new();
    private bool _suppressPeriodEvent;
    private readonly Border _hdrStatusPill = new();
    private readonly TextBlock _hdrStatusText = new();
    private readonly Button _btnSalvarAlteracoes = new();

    // Navigation & host
    private readonly Dictionary<NavSection, Button> _navButtons = new();
    private readonly TextBlock _badgeFaltantes = new();
    private readonly Border _viewHost = new();

    // Status bar controls
    private readonly TextBlock _statusLeftText = new();
    private readonly TextBlock _statusDbText = new();
    private readonly ProgressBar _statusDbBar = new();

    // Cached WCAG AA verified static brushes & design tokens
    private static readonly IBrush BrushTextDark = Brush.Parse("#0F172A");
    private static readonly IBrush BrushTextBody = Brush.Parse("#334155");
    private static readonly IBrush BrushMuted = Brush.Parse("#475569");
    private static readonly IBrush BrushBlueDark = Brush.Parse("#1D4ED8");
    private static readonly IBrush BrushBlueInfo = Brush.Parse("#0369A1");
    private static readonly IBrush BrushGreen = Brush.Parse("#15803D");
    private static readonly IBrush BrushAmber = Brush.Parse("#B45309");
    private static readonly IBrush BrushRed = Brush.Parse("#B91C1C");
    private static readonly IBrush BrushBorderLight = Brush.Parse("#E2E8F0");
    private static readonly IBrush BrushHeaderBg = Brush.Parse("#F1F5F9");
    private static readonly IBrush BrushErrorBg = Brush.Parse("#FEF2F2");
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Thickness CellPad = new(8, 6);

    public MainWindow(string? initialFilePath = null, string? initialAuditXmlPath = null)
    {
        Title = $"OpenSintegra Contábil v{AppVersion}";
        Icon = VectorIcons.AppWindowIcon;
        Width = 1340;
        Height = 840;
        MinWidth = 1020;
        MinHeight = 660;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush.Parse("#EEF2F6");

        BuildShellLayout();
        SetEmptyWelcomeState();

        SizeChanged += (_, e) =>
        {
            bool nextCompact = e.NewSize.Width > 100 && e.NewSize.Width < 1180;
            if (nextCompact != _isCompactLayout)
            {
                _isCompactLayout = nextCompact;
                if (_activeSection == NavSection.VisaoGeral && _hasDocument)
                    RenderActiveSection();
            }
        };

        Opened += async (_, _) =>
        {
            string? sintegraToLoad = (!string.IsNullOrWhiteSpace(initialFilePath) && File.Exists(initialFilePath))
                ? initialFilePath
                : _db.GetSetting("last_sintegra_path");

            string? xmlToLoad = (!string.IsNullOrWhiteSpace(initialAuditXmlPath) && (File.Exists(initialAuditXmlPath) || Directory.Exists(initialAuditXmlPath)))
                ? initialAuditXmlPath
                : _db.GetSetting("last_xml_path");

            if (!string.IsNullOrWhiteSpace(sintegraToLoad) && File.Exists(sintegraToLoad))
            {
                await LoadSintegraFileAsync(sintegraToLoad);
                if (!string.IsNullOrWhiteSpace(xmlToLoad) && (File.Exists(xmlToLoad) || Directory.Exists(xmlToLoad)))
                    await LoadXmlOrZipAsync(xmlToLoad);
            }
        };
    }

    private void BuildShellLayout()
    {
        var outerGrid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };

        var menuBar = BuildMenuBar();
        Grid.SetRow(menuBar, 0);
        outerGrid.Children.Add(menuBar);

        var toolbar = BuildRibbonToolbar();
        Grid.SetRow(toolbar, 1);
        outerGrid.Children.Add(toolbar);

        var bodyGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("208,*") };

        var sidebar = BuildLeftSidebar();
        Grid.SetColumn(sidebar, 0);
        bodyGrid.Children.Add(sidebar);

        var workspaceScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Background = Brush.Parse("#EEF2F6")
        };

        var workspaceContainer = new StackPanel
        {
            Margin = new Thickness(14, 12, 14, 14),
            Spacing = 12
        };
        workspaceContainer.Children.Add(BuildCompanyHeaderCard());
        workspaceContainer.Children.Add(_viewHost);

        workspaceScroll.Content = workspaceContainer;
        Grid.SetColumn(workspaceScroll, 1);
        bodyGrid.Children.Add(workspaceScroll);

        Grid.SetRow(bodyGrid, 2);
        outerGrid.Children.Add(bodyGrid);

        var statusBar = BuildStatusBar();
        Grid.SetRow(statusBar, 3);
        outerGrid.Children.Add(statusBar);

        Content = outerGrid;
    }

    private Control BuildMenuBar()
    {
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#D5DEE8"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 2)
        };

        var menu = new Menu { Background = Brushes.Transparent, FontSize = 12.5 };

        var mArquivo = new MenuItem { Header = "Arquivo" };
        var miAbrir = new MenuItem { Header = "Abrir Sintegra (.txt)..." };
        miAbrir.Click += async (_, _) => await PickAndOpenSintegraAsync();
        var miAbrirXml = new MenuItem { Header = "Carregar XML / ZIP de Compras..." };
        miAbrirXml.Click += async (_, _) => await PickAndOpenXmlAsync();
        var miAbrirXmlFolder = new MenuItem { Header = "Carregar Pasta de XMLs (inclui subpastas)..." };
        miAbrirXmlFolder.Click += async (_, _) => await PickAndOpenXmlFolderAsync();
        var miAbrirSped = new MenuItem { Header = "Carregar SPED Fiscal (.txt)..." };
        miAbrirSped.Click += async (_, _) => await PickAndOpenSpedAsync();
        var miSalvar = new MenuItem { Header = "Salvar Sintegra (.txt)..." };
        miSalvar.Click += async (_, _) => await SaveCurrentBytesDialogAsync();
        var miDividir = new MenuItem { Header = "Dividir Sintegra Anual por Mês..." };
        miDividir.Click += async (_, _) => await ExportMonthlySplitsDialogAsync();
        var miSair = new MenuItem { Header = "Sair" };
        miSair.Click += (_, _) => Close();
        mArquivo.Items.Add(miAbrir);
        mArquivo.Items.Add(miAbrirXml);
        mArquivo.Items.Add(miAbrirXmlFolder);
        mArquivo.Items.Add(miAbrirSped);
        mArquivo.Items.Add(new Separator());
        mArquivo.Items.Add(miSalvar);
        mArquivo.Items.Add(miDividir);
        mArquivo.Items.Add(new Separator());
        mArquivo.Items.Add(miSair);

        var mFiscal = new MenuItem { Header = "Fiscal" };
        var miLancar = new MenuItem { Header = "Lançar Notas Faltantes no Sintegra..." };
        miLancar.Click += async (_, _) => await OpenInjectMissingDialogAsync(null);
        var miReproc = new MenuItem { Header = "Reprocessar XMLs (Datas de Entrada / Faltantes)..." };
        miReproc.Click += async (_, _) => await ReloadAndReprocessLastXmlAsync();
        var miAutoFix = new MenuItem { Header = "Corrigir Erros Estruturais (Auto-Fix)" };
        miAutoFix.Click += async (_, _) => await RunQuickFixAndRefreshAsync();
        var miCfop = new MenuItem { Header = "Resumo CFOP e Produtos Mais Vendidos" };
        miCfop.Click += (_, _) => NavigateTo(NavSection.CfopEProdutos);
        mFiscal.Items.Add(miLancar);
        mFiscal.Items.Add(miReproc);
        mFiscal.Items.Add(miAutoFix);
        mFiscal.Items.Add(miCfop);

        var mRel = new MenuItem { Header = "Relatórios" };
        var miPrevComp = new MenuItem { Header = "Visualizar Relatório Completo (Entradas x Saídas)..." };
        miPrevComp.Click += (_, _) => OpenReportPreview(ReportMode.Completo);
        var miPrevMiss = new MenuItem { Header = "Relatório de Notas Não Lançadas (Para Cliente)..." };
        miPrevMiss.Click += (_, _) => OpenReportPreview(ReportMode.SomenteNotasNaoLancadas);
        var miPrevProd = new MenuItem { Header = "Ranking de Produtos Mais Vendidos..." };
        miPrevProd.Click += (_, _) => OpenReportPreview(ReportMode.ProdutosMaisVendidos);
        var miPdf = new MenuItem { Header = "Exportar PDF..." };
        miPdf.Click += async (_, _) => await ExportDirectPdfAsync();
        var miXlsx = new MenuItem { Header = "Exportar Excel (.xlsx)..." };
        miXlsx.Click += async (_, _) => await ExportDirectXlsxAsync();
        mRel.Items.Add(miPrevComp);
        mRel.Items.Add(miPrevMiss);
        mRel.Items.Add(miPrevProd);
        mRel.Items.Add(new Separator());
        mRel.Items.Add(miPdf);
        mRel.Items.Add(miXlsx);

        var mAjuda = new MenuItem { Header = "Ajuda" };
        var miDemo = new MenuItem { Header = "Carregar Dados de Exemplo (Teste)" };
        miDemo.Click += (_, _) => LoadDemoExample();
        var miConfig = new MenuItem { Header = "Configurações e Limite SQLite" };
        miConfig.Click += (_, _) => NavigateTo(NavSection.EmpresasEBanco);
        var miSobre = new MenuItem { Header = "Sobre" };
        miSobre.Click += async (_, _) => await ShowInfoDialogAsync(
            "OpenSintegra Contábil",
            $"OpenSintegra Contábil v{AppVersion} (Licença MIT)\n\n" +
            "Validação, correção, lançamento de notas XML faltantes e relatórios gerenciais do Sintegra.");
        mAjuda.Items.Add(miConfig);
        mAjuda.Items.Add(miDemo);
        mAjuda.Items.Add(new Separator());
        mAjuda.Items.Add(miSobre);

        menu.Items.Add(mArquivo);
        menu.Items.Add(mFiscal);
        menu.Items.Add(mRel);
        menu.Items.Add(mAjuda);

        border.Child = menu;
        return border;
    }

    private Control BuildRibbonToolbar()
    {
        var bar = new Border
        {
            Background = Brush.Parse("#F8FAFC"),
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 6)
        };

        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        strip.Children.Add(CreateRibbonButton("Abrir", "Sintegra", VectorIcons.FolderOpen, "#2563EB", async () => await PickAndOpenSintegraAsync()));

        var btnXmlRibbon = CreateRibbonButton("Carregar", "XML / Pasta", VectorIcons.UploadZip, "#0369A1", null);
        var xmlFlyout = BuildXmlActionsFlyout();
        btnXmlRibbon.Click += (_, _) => xmlFlyout.ShowAt(btnXmlRibbon);
        strip.Children.Add(btnXmlRibbon);

        strip.Children.Add(CreateToolbarDivider());
        strip.Children.Add(CreateRibbonButton("Lançar", "Faltantes", VectorIcons.CheckBadge, "#15803D", async () => await OpenInjectMissingDialogAsync(null)));
        strip.Children.Add(CreateRibbonButton("Corrigir", "Sintegra", VectorIcons.Wrench, "#B45309", async () => await RunQuickFixAndRefreshAsync()));
        strip.Children.Add(CreateToolbarDivider());

        var btnRel = CreateRibbonButton("Relatórios", "PDF / Excel", VectorIcons.DocumentText, "#2563EB", null);
        var relFlyout = new MenuFlyout();
        var fComp = new MenuItem { Header = "Visualizar Relatório Completo (Entradas x Saídas)..." };
        fComp.Click += (_, _) => OpenReportPreview(ReportMode.Completo);
        var fMiss = new MenuItem { Header = "Visualizar Somente Notas Não Lançadas..." };
        fMiss.Click += (_, _) => OpenReportPreview(ReportMode.SomenteNotasNaoLancadas);
        var fProd = new MenuItem { Header = "Visualizar Produtos Mais Vendidos..." };
        fProd.Click += (_, _) => OpenReportPreview(ReportMode.ProdutosMaisVendidos);
        var fPdf = new MenuItem { Header = "Exportar PDF Direto..." };
        fPdf.Click += async (_, _) => await ExportDirectPdfAsync();
        var fXlsx = new MenuItem { Header = "Exportar Excel (.xlsx)..." };
        fXlsx.Click += async (_, _) => await ExportDirectXlsxAsync();
        relFlyout.Items.Add(fComp);
        relFlyout.Items.Add(fMiss);
        relFlyout.Items.Add(fProd);
        relFlyout.Items.Add(new Separator());
        relFlyout.Items.Add(fPdf);
        relFlyout.Items.Add(fXlsx);
        btnRel.Click += (_, _) => relFlyout.ShowAt(btnRel);
        strip.Children.Add(btnRel);

        strip.Children.Add(CreateRibbonButton("Empresas", "e Banco", VectorIcons.SettingsGear, "#475569", () => NavigateTo(NavSection.EmpresasEBanco)));

        bar.Child = strip;
        return bar;
    }

    private static Button CreateRibbonButton(string line1, string line2, string iconPath, string iconHex, Action? onClick)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 3 };
        stack.Children.Add(VectorIcons.Create(iconPath, iconHex, 20));
        stack.Children.Add(new TextBlock
        {
            Text = $"{line1}\n{line2}",
            FontSize = 11.5,
            Foreground = Brush.Parse("#334155"),
            TextAlignment = TextAlignment.Center,
            LineHeight = 13.5
        });

        var btn = new Button
        {
            Content = stack
        };
        btn.Classes.Add("ribbon");
        if (onClick is not null)
            btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Control CreateToolbarDivider() => new Border
    {
        Width = 1,
        Height = 40,
        Margin = new Thickness(2),
        Background = Brush.Parse("#CBD5E1"),
        VerticalAlignment = VerticalAlignment.Center
    };

    private Control BuildLeftSidebar()
    {
        var container = new Border
        {
            Background = Brush.Parse("#F8FAFC"),
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(10, 12)
        };

        var rootGrid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        var navStack = new StackPanel { Spacing = 5 };

        navStack.Children.Add(CreateNavButton(NavSection.VisaoGeral, "Visão Geral", VectorIcons.Home, true));
        navStack.Children.Add(CreateNavButton(NavSection.NotasFaltantes, "Notas Faltantes (XML)", VectorIcons.Warning, false, _badgeFaltantes));
        navStack.Children.Add(CreateNavButton(NavSection.NotasFiscais, "Notas Fiscais", VectorIcons.DocumentText, false));
        navStack.Children.Add(CreateNavButton(NavSection.CfopEProdutos, "CFOP e Produtos", VectorIcons.ChartBar, false));
        navStack.Children.Add(CreateNavButton(NavSection.CorrecaoEExportacao, "Corrigir e Exportar", VectorIcons.Wrench, false));
        navStack.Children.Add(CreateNavButton(NavSection.EmpresasEBanco, "Empresas e Banco", VectorIcons.Database, false));

        Grid.SetRow(navStack, 0);
        rootGrid.Children.Add(navStack);

        var bottomBox = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = "Convênio ICMS 57/95", FontSize = 11.5, FontWeight = FontWeight.SemiBold, Foreground = BrushTextDark },
                    new TextBlock { Text = $"OpenSintegra v{AppVersion} MIT", FontSize = 11, Foreground = BrushMuted }
                }
            }
        };
        Grid.SetRow(bottomBox, 1);
        rootGrid.Children.Add(bottomBox);

        container.Child = rootGrid;
        return container;
    }

    private Button CreateNavButton(NavSection section, string label, string iconPath, bool isPrimary, TextBlock? rightBadge = null)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var ico = VectorIcons.Create(iconPath, isPrimary ? "#1D4ED8" : "#475569", 16);
        ico.Margin = new Thickness(0, 0, 9, 0);
        Grid.SetColumn(ico, 0);
        grid.Children.Add(ico);

        var lbl = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            FontWeight = isPrimary ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = Brush.Parse(isPrimary ? "#1E3A8A" : "#334155"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(lbl, 1);
        grid.Children.Add(lbl);

        if (rightBadge is not null)
        {
            rightBadge.FontSize = 11;
            rightBadge.FontWeight = FontWeight.Bold;
            rightBadge.Foreground = BrushRed;
            rightBadge.VerticalAlignment = VerticalAlignment.Center;
            rightBadge.IsVisible = false;
            Grid.SetColumn(rightBadge, 2);
            grid.Children.Add(rightBadge);
        }

        var btn = new Button
        {
            Content = grid
        };
        btn.Classes.Add("nav");
        if (isPrimary)
            btn.Classes.Add("active");
        btn.Click += (_, _) => NavigateTo(section);
        _navButtons[section] = btn;
        return btn;
    }

    private Control BuildCompanyHeaderCard()
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#D5DEE8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10)
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto") };

        var companyGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var icoBox = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse("#EFF6FF"),
            BorderBrush = Brush.Parse("#BFDBFE"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 10, 0),
            Child = VectorIcons.Create(VectorIcons.Building, "#1E40AF", 20)
        };
        Grid.SetColumn(icoBox, 0);
        companyGrid.Children.Add(icoBox);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _hdrCompanyName.FontSize = 14;
        _hdrCompanyName.FontWeight = FontWeight.Bold;
        _hdrCompanyName.Foreground = BrushTextDark;
        _hdrCompanyName.TextTrimming = TextTrimming.CharacterEllipsis;
        _hdrCompanyDocs.FontSize = 11.5;
        _hdrCompanyDocs.Foreground = Brush.Parse("#475569");
        _hdrCompanyDocs.TextTrimming = TextTrimming.CharacterEllipsis;
        texts.Children.Add(_hdrCompanyName);
        texts.Children.Add(_hdrCompanyDocs);
        Grid.SetColumn(texts, 1);
        companyGrid.Children.Add(texts);
        Grid.SetColumn(companyGrid, 0);
        grid.Children.Add(companyGrid);

        var rightWrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        var periodPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 2, 10, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var lblPeriod = new TextBlock
        {
            Text = "Competência:",
            FontSize = 12,
            Foreground = Brush.Parse("#475569"),
            VerticalAlignment = VerticalAlignment.Center
        };
        periodPanel.Children.Add(lblPeriod);
        _cmbPeriod.MinWidth = 185;
        _cmbPeriod.MaxWidth = 240;
        _cmbPeriod.FontSize = 12;
        _cmbPeriod.SelectionChanged += (_, _) =>
        {
            if (_suppressPeriodEvent) return;
            if (_cmbPeriod.SelectedItem is ComboBoxItem cbi && cbi.Tag is string ym)
            {
                _selectedYearMonth = ym;
                RebuildScopedAndRefresh();
            }
        };
        periodPanel.Children.Add(_cmbPeriod);
        rightWrap.Children.Add(periodPanel);

        _hdrStatusPill.CornerRadius = new CornerRadius(14);
        _hdrStatusPill.Padding = new Thickness(12, 5);
        _hdrStatusPill.Margin = new Thickness(0, 2, 10, 2);
        _hdrStatusPill.VerticalAlignment = VerticalAlignment.Center;
        _hdrStatusText.FontSize = 12;
        _hdrStatusText.FontWeight = FontWeight.SemiBold;
        _hdrStatusPill.Child = _hdrStatusText;
        rightWrap.Children.Add(_hdrStatusPill);

        _btnSalvarAlteracoes.Classes.Add("success");
        _btnSalvarAlteracoes.Margin = new Thickness(0, 2, 0, 2);
        _btnSalvarAlteracoes.VerticalAlignment = VerticalAlignment.Center;
        _btnSalvarAlteracoes.IsVisible = false;
        _btnSalvarAlteracoes.Content = new TextBlock
        {
            Text = "Salvar Sintegra Atualizado",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        _btnSalvarAlteracoes.Click += async (_, _) => await SaveCurrentBytesDialogAsync();
        rightWrap.Children.Add(_btnSalvarAlteracoes);

        Grid.SetColumn(rightWrap, 2);
        grid.Children.Add(rightWrap);

        card.Child = grid;
        return card;
    }

    private Control BuildStatusBar()
    {
        var bar = new Border
        {
            Background = Brush.Parse("#F8FAFC"),
            BorderBrush = Brush.Parse("#CBD5E1"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 5)
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        _statusLeftText.FontSize = 11.5;
        _statusLeftText.Foreground = BrushTextBody;
        _statusLeftText.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_statusLeftText, 0);
        grid.Children.Add(_statusLeftText);

        var dbStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        dbStack.Children.Add(VectorIcons.Create(VectorIcons.Database, "#475569", 14));
        _statusDbText.FontSize = 11.5;
        _statusDbText.Foreground = BrushTextBody;
        _statusDbText.VerticalAlignment = VerticalAlignment.Center;
        dbStack.Children.Add(_statusDbText);

        _statusDbBar.Minimum = 0;
        _statusDbBar.Maximum = 100;
        _statusDbBar.Width = 96;
        _statusDbBar.Height = 8;
        _statusDbBar.Foreground = Brush.Parse("#2563EB");
        _statusDbBar.Background = Brush.Parse("#E2E8F0");
        _statusDbBar.CornerRadius = new CornerRadius(4);
        _statusDbBar.VerticalAlignment = VerticalAlignment.Center;
        dbStack.Children.Add(_statusDbBar);

        Grid.SetColumn(dbStack, 1);
        grid.Children.Add(dbStack);

        bar.Child = grid;
        return bar;
    }

    // ==================== STATE & DATA LOADING ====================

    private void SetEmptyWelcomeState()
    {
        _hasDocument = false;
        _hasUnsavedChanges = false;
        _currentFilePath = "";
        _currentFileName = "Nenhum arquivo aberto";
        _currentBytes = Array.Empty<byte>();
        _doc = new SintegraDocument();
        _report = SintegraValidator.Validate(_doc);
        _audit = null;
        _loadedXmlDocs = Array.Empty<XmlDocumentSummary>();
        _loadedXmlSourcePaths.Clear();
        _selectedYearMonth = "ALL";
        PopulatePeriodComboBox();
        RebuildScopedAndRefresh();
    }

    private void LoadDemoExample()
    {
        static string F50(string ymd, string num, string cfop, long val) =>
            $"50000000000191{"ISENTO",-14}{ymd}MS55001{num,-6}{cfop}T{val:0000000000000}{val:0000000000000}{0:0000000000000}{0:0000000000000}{0:0000000000000}0000N";
        static string F61(string ymd, long val) =>
            $"61{new string(' ', 28)}{ymd}65001  000001000050{val:0000000000000}{val:0000000000000}{0:000000000000}{0:0000000000000}{0:0000000000000}0000 ";

        var sb = new StringBuilder();
        sb.Append($"1011222333000181{"ISENTO",-14}{"EMPRESA DEMONSTRACAO SINTEGRA",-35}{"CAMPO GRANDE",-30}MS00000000002026010120260228331\r\n");
        sb.Append($"11{"RUA EXEMPLO",-34}00100{"SALA 1",-22}{"CENTRO",-15}79002000{"CONTADOR",-28}067330011220\r\n");
        sb.Append(F50("20260115", "000010", "1102", 2_000_000)).Append("\r\n");
        sb.Append(F50("20260215", "000020", "1102", 2_500_000)).Append("\r\n");
        sb.Append(F61("20260131", 3_100_000)).Append("\r\n");
        sb.Append(F61("20260228", 3_400_000)).Append("\r\n");
        sb.Append($"752026010120260228{"PROD_ORFAO",-14}08051000{"ITEM EXEMPLO ORFAO",-53}{"UN",-6}000000000000000000000000000\r\n");
        sb.Append($"9011222333000181{"ISENTO",-14}{"50000000019900000001",-95}1\r\n");

        _hasDocument = true;
        _hasUnsavedChanges = false;
        _currentFilePath = "";
        _currentFileName = "Exemplo_Demonstracao.txt";
        _currentBytes = Encoding.Latin1.GetBytes(sb.ToString());
        _doc = SintegraParser.ParseBytes(_currentBytes);
        _report = SintegraValidator.Validate(_doc);
        _audit = null;
        _loadedXmlDocs = Array.Empty<XmlDocumentSummary>();
        _loadedXmlSourcePaths.Clear();
        _selectedYearMonth = "ALL";
        PopulatePeriodComboBox();
        RebuildScopedAndRefresh();
    }

    private void PopulatePeriodComboBox()
    {
        _suppressPeriodEvent = true;
        _cmbPeriod.Items.Clear();

        if (!_hasDocument)
        {
            _cmbPeriod.Items.Add(new ComboBoxItem { Content = "Nenhum arquivo aberto", Tag = "ALL" });
            _cmbPeriod.SelectedIndex = 0;
            _suppressPeriodEvent = false;
            return;
        }

        string fullLabel = _doc.Reg10 is { } r10
            ? $"{TaxIdValidator.FormatDateBr(r10.DataInicial)} a {TaxIdValidator.FormatDateBr(r10.DataFinal)}"
            : "Todos os meses";

        _cmbPeriod.Items.Add(new ComboBoxItem
        {
            Content = $"{fullLabel} (Todos - {_report.MonthlyBalance.Count} mês(es))",
            Tag = "ALL"
        });

        foreach (var mb in _report.MonthlyBalance)
        {
            _cmbPeriod.Items.Add(new ComboBoxItem
            {
                Content = $"{mb.MonthLabel}",
                Tag = mb.YearMonth
            });
        }

        int sel = 0;
        for (int i = 0; i < _cmbPeriod.Items.Count; i++)
        {
            if (_cmbPeriod.Items[i] is ComboBoxItem cbi && Equals(cbi.Tag, _selectedYearMonth))
            {
                sel = i;
                break;
            }
        }
        _cmbPeriod.SelectedIndex = sel;
        _suppressPeriodEvent = false;
    }

    private void RebuildScopedAndRefresh()
    {
        _scoped = PeriodFilter.CreateScopedView(_doc, _report, _audit, _selectedYearMonth);
        UpdateHeaderAndStatusBar();
        RenderActiveSection();
    }

    private void UpdateHeaderAndStatusBar()
    {
        if (!_hasDocument)
        {
            _hdrCompanyName.Text = "OpenSintegra Contábil";
            _hdrCompanyDocs.Text = "Abra um arquivo Sintegra (.txt) e o ZIP/pasta de XMLs para conferir entradas x saídas.";
            _hdrStatusPill.Background = Brush.Parse("#F1F5F9");
            _hdrStatusText.Foreground = BrushMuted;
            _hdrStatusText.Text = "Aguardando arquivo";
            _btnSalvarAlteracoes.IsVisible = false;
            _badgeFaltantes.IsVisible = false;
            _statusLeftText.Text = "Pronto";
        }
        else
        {
            var r10 = _doc.Reg10;
            string companyName = r10 is not null ? r10.NomeContribuinte.Trim() : _currentFileName;
            string cnpj = r10 is not null ? TaxIdValidator.FormatCnpj(r10.Cnpj) : "-";
            string ie = r10 is not null ? r10.Ie.Trim() : "-";
            string regime = r10 is not null ? _db.GetCompanyTaxRegime(r10.Cnpj) : "Simples Nacional";

            _hdrCompanyName.Text = companyName;
            _hdrCompanyDocs.Text = $"CNPJ: {cnpj}  |  IE: {ie}  |  Regime: {regime}";

            int err = _report.Summary.ErrorCount;
            int warn = _report.Summary.WarningCount;
            if (err > 0)
            {
                _hdrStatusPill.Background = Brush.Parse("#FEE2E2");
                _hdrStatusText.Foreground = Brush.Parse("#B91C1C");
                _hdrStatusText.Text = $"{err} erro(s) no Sintegra";
            }
            else if (warn > 0)
            {
                _hdrStatusPill.Background = Brush.Parse("#DCFCE7");
                _hdrStatusText.Foreground = BrushGreen;
                _hdrStatusText.Text = $"Sintegra válido ({warn} avisos)";
            }
            else
            {
                _hdrStatusPill.Background = Brush.Parse("#DCFCE7");
                _hdrStatusText.Foreground = BrushGreen;
                _hdrStatusText.Text = "Sintegra válido";
            }

            _btnSalvarAlteracoes.IsVisible = _hasUnsavedChanges;

            int missingCount = _scoped.XmlComprasOmissas;
            _badgeFaltantes.Text = missingCount > 0 ? missingCount.ToString() : "";
            _badgeFaltantes.IsVisible = missingCount > 0;

            _statusLeftText.Text = $"Arquivo: {_currentFileName}  |  Período: {_scoped.PeriodLabel}  |  {_doc.Reg50.Count} NF Reg.50  |  {_doc.Reg61.Count} Reg.61";
        }

        var stats = _db.GetStorageStats();
        _statusDbText.Text = stats.FormattedSummary;
        _statusDbBar.Value = Math.Clamp(stats.UsageFraction * 100.0, 0, 100);
        _statusDbBar.Foreground = stats.UsageFraction > 0.85 ? BrushRed : Brush.Parse("#2563EB");
    }

    private void NavigateTo(NavSection section)
    {
        _activeSection = section;
        foreach (var (sec, btn) in _navButtons)
        {
            bool active = sec == section;
            btn.Classes.Set("active", active);
            if (btn.Content is Grid g && g.Children.Count >= 2)
            {
                if (g.Children[0] is PathIcon pi)
                    pi.Foreground = Brush.Parse(active ? "#1D4ED8" : "#475569");
                if (g.Children[1] is TextBlock tb)
                {
                    tb.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
                    tb.Foreground = Brush.Parse(active ? "#1E3A8A" : "#334155");
                }
            }
        }
        RenderActiveSection();
    }

    private void RenderActiveSection()
    {
        if (!_hasDocument && _activeSection != NavSection.EmpresasEBanco)
        {
            _viewHost.Child = BuildWelcomeView();
            return;
        }

        _viewHost.Child = _activeSection switch
        {
            NavSection.VisaoGeral => BuildDashboardView(),
            NavSection.NotasFaltantes => BuildMissingInvoicesView(),
            NavSection.NotasFiscais => BuildDocumentsView(),
            NavSection.CfopEProdutos => BuildCfopAndProductsView(),
            NavSection.CorrecaoEExportacao => BuildFixAndExportView(),
            NavSection.EmpresasEBanco => BuildCompanyAndDbView(),
            _ => BuildDashboardView()
        };
    }

    // ==================== WELCOME SCREEN ====================

    private Control BuildWelcomeView()
    {
        var card = CreateCardShell("Bem-vindo ao OpenSintegra Contábil", VectorIcons.FolderOpen, null);
        var stack = (StackPanel)card.Child!;
        stack.Spacing = 12;
        stack.Margin = new Thickness(0, 8, 0, 8);

        stack.Children.Add(new TextBlock
        {
            Text = "Importe seu arquivo Sintegra (.txt mensal ou anual) e opcionalmente o pacote ZIP/pasta com os XMLs de compras para:\n" +
                   "  - Comparar Entradas x Saídas mês a mês em linguagem simples para o escritório e o cliente;\n" +
                   "  - Descobrir exatamente quais notas fiscais de compra ficaram sem lançamento e em quais meses;\n" +
                   "  - Lançar as notas XML faltantes diretamente no Sintegra e salvar o arquivo pronto e validado.",
            FontSize = 13,
            Foreground = BrushTextBody,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20
        });

        var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        btns.Children.Add(CreatePrimaryButton("Abrir Sintegra (.txt)...", VectorIcons.FolderOpen, async () => await PickAndOpenSintegraAsync()));
        var btnXml = CreateQuickActionButton("Carregar XML / ZIP / Pasta...", VectorIcons.UploadZip, null);
        var welcomeXmlFlyout = BuildXmlActionsFlyout(includeReprocess: false);
        btnXml.Click += (_, _) => welcomeXmlFlyout.ShowAt(btnXml);
        btnXml.HorizontalAlignment = HorizontalAlignment.Left;
        btns.Children.Add(btnXml);
        stack.Children.Add(btns);

        return card;
    }

    // ==================== 1. VISAO GERAL ====================

    private Control BuildDashboardView()
    {
        var root = new StackPanel { Spacing = 12 };
        bool compact = IsCompactWidth;

        // Row 1: Responsive KPI Cards (3 cols when wide, 2+1 cols when compact <= 1180px)
        var kpiGrid = compact
            ? new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*"), RowDefinitions = new RowDefinitions("Auto,12,Auto") }
            : new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*") };

        var cardEntradas = BuildKpiCard(
            VectorIcons.ArrowDown, "#2563EB", "#DBEAFE",
            "Entradas no Sintegra",
            $"R$ {TaxIdValidator.FormatCentsBrl(_scoped.TotalEntradasCents)}",
            $"Compras p/ revenda: R$ {TaxIdValidator.FormatCentsBrl(_scoped.ComprasEntradasCents)}  |  Outras: R$ {TaxIdValidator.FormatCentsBrl(_scoped.OutrasEntradasCents)}",
            null);
        Grid.SetColumn(cardEntradas, 0);
        Grid.SetRow(cardEntradas, 0);
        kpiGrid.Children.Add(cardEntradas);

        string saidasDetail = _scoped.SaidasReg50Cents > 0
            ? $"Cupons NFC-e: R$ {TaxIdValidator.FormatCentsBrl(_scoped.SaidasReg61Cents)}  |  NF-e: R$ {TaxIdValidator.FormatCentsBrl(_scoped.SaidasReg50Cents)}"
            : $"Cupons NFC-e (Reg. 61): R$ {TaxIdValidator.FormatCentsBrl(_scoped.SaidasReg61Cents)}";

        var cardSaidas = BuildKpiCard(
            VectorIcons.ArrowUp, "#15803D", "#DCFCE7",
            "Saídas / Vendas no Sintegra",
            $"R$ {TaxIdValidator.FormatCentsBrl(_scoped.TotalSaidasCents)}",
            saidasDetail,
            null);
        Grid.SetColumn(cardSaidas, 2);
        Grid.SetRow(cardSaidas, 0);
        kpiGrid.Children.Add(cardSaidas);

        bool salesHigher = _scoped.TotalSaidasCents >= _scoped.TotalEntradasCents;
        string diffBadge = salesHigher ? "Saídas > Entradas" : "Entradas > Saídas";

        string kpi3Sub = _audit is null
            ? "Diferença: Saídas menos Entradas"
            : _scoped.XmlComprasOmissas > 0
                ? $"Faltam {_scoped.XmlComprasOmissas} XML(s) (R$ {TaxIdValidator.FormatCentsBrl(_scoped.XmlComprasOmissasCents)})"
                : "Todas as compras XML lançadas";

        var cardRatio = BuildKpiCard(
            VectorIcons.Scale, salesHigher ? "#15803D" : "#B91C1C", salesHigher ? "#DCFCE7" : "#FEE2E2",
            "Saldo (Saídas - Entradas)",
            TaxIdValidator.FormatSignedCentsBrl(_scoped.SaldoLiquidoCents),
            kpi3Sub,
            (diffBadge, salesHigher ? "#DCFCE7" : "#FEE2E2", salesHigher ? "#15803D" : "#B91C1C"),
            salesHigher ? "#15803D" : "#B91C1C");
        if (compact)
        {
            Grid.SetColumn(cardRatio, 0);
            Grid.SetColumnSpan(cardRatio, 3);
            Grid.SetRow(cardRatio, 2);
        }
        else
        {
            Grid.SetColumn(cardRatio, 4);
        }
        kpiGrid.Children.Add(cardRatio);

        root.Children.Add(kpiGrid);

        // Row 2: Charts + Alerts Sidebar (reflow cleanly when compact)
        var lowerGrid = compact
            ? new Grid { RowDefinitions = new RowDefinitions("Auto,12,Auto") }
            : new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,285") };
        var leftCol = new StackPanel { Spacing = 12 };

        var chartsRow = compact
            ? new Grid { RowDefinitions = new RowDefinitions("Auto,12,Auto") }
            : new Grid { ColumnDefinitions = new ColumnDefinitions("1.42*,12,*") };
        var barCard = CreateCardShell("Evolução Mensal: Entradas vs Saídas", VectorIcons.ChartBar, BuildBarLegend());
        var barChart = new MonthlyBarChartControl { Height = 190 };
        barChart.SetData(_report.MonthlyBalance.ToList(), _scoped.IsSingleMonthFilter ? _scoped.SelectedYearMonth : null);
        ((StackPanel)barCard.Child!).Children.Add(barChart);
        Grid.SetColumn(barCard, 0);
        Grid.SetRow(barCard, 0);
        chartsRow.Children.Add(barCard);

        var donutCard = CreateCardShell("Comparativo do Período", VectorIcons.ChartBar, null);
        var donutChart = new CompositionDonutControl { Width = 136, Height = 136 };
        donutChart.SetData(_scoped.TotalEntradasCents, _scoped.TotalSaidasCents);

        var donutLayout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(4, 14, 4, 8),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(donutChart, 0);
        donutLayout.Children.Add(donutChart);

        var legendStack = new StackPanel { Spacing = 14, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        legendStack.Children.Add(CreateDonutLegendItem("#15803D", "Saídas (Vendas)", $"R$ {TaxIdValidator.FormatCentsBrl(_scoped.TotalSaidasCents)}", ""));
        legendStack.Children.Add(CreateDonutLegendItem("#2563EB", "Entradas", $"R$ {TaxIdValidator.FormatCentsBrl(_scoped.TotalEntradasCents)}", ""));
        Grid.SetColumn(legendStack, 1);
        donutLayout.Children.Add(legendStack);

        ((StackPanel)donutCard.Child!).Children.Add(donutLayout);
        if (compact)
        {
            Grid.SetColumn(donutCard, 0);
            Grid.SetRow(donutCard, 2);
        }
        else
        {
            Grid.SetColumn(donutCard, 2);
        }
        chartsRow.Children.Add(donutCard);

        leftCol.Children.Add(chartsRow);

        // Monthly Entradas x Saidas x XML Faltantes Table
        var tableCard = CreateCardShell("Entradas e Saídas por Mês", VectorIcons.DocumentText, BuildMonthlyFilterHintBadge());
        ((StackPanel)tableCard.Child!).Children.Add(BuildMonthlyComparisonTable(_scoped.MonthlyRows));
        leftCol.Children.Add(tableCard);

        Grid.SetColumn(leftCol, 0);
        Grid.SetRow(leftCol, 0);
        lowerGrid.Children.Add(leftCol);

        // Right Sidebar inside Dashboard: Focused Pendencias + Direct Client Action
        var alertsCard = CreateCardShell("Situação e Pendências", VectorIcons.Warning, null);
        var alertsBody = (StackPanel)alertsCard.Child!;

        if (_audit is null)
        {
            alertsBody.Children.Add(CreateAlertItem(
                false,
                "Conferir XMLs de compras",
                "Carregue o ZIP ou pasta de XMLs para verificar quais notas não estão lançadas.",
                async () => await PickAndOpenXmlAsync()));
        }
        else if (_scoped.XmlComprasOmissas > 0)
        {
            int distinctMonthsWithGaps = _scoped.XmlDocumentRows.Where(r => r.IsCriticalGap).Select(r => r.YearMonth).Distinct().Count();
            alertsBody.Children.Add(CreateAlertItem(
                true,
                $"{_scoped.XmlComprasOmissas} nota(s) não lançada(s)",
                $"R$ {TaxIdValidator.FormatCentsBrl(_scoped.XmlComprasOmissasCents)} em {distinctMonthsWithGaps} mês(es). Clique para ver e lançar.",
                () => NavigateTo(NavSection.NotasFaltantes)));
        }
        else
        {
            alertsBody.Children.Add(CreateAlertItem(
                false,
                "Entradas 100% lançadas",
                "Todas as compras dos XMLs do período estão no Sintegra.",
                () => NavigateTo(NavSection.NotasFaltantes)));
        }

        int errCount = _report.Summary.ErrorCount;
        int warnCount = _report.Summary.WarningCount;
        alertsBody.Children.Add(CreateAlertItem(
            errCount > 0,
            errCount > 0 ? $"{errCount} erro(s) estruturais no Sintegra" : $"Sintegra estrutural OK ({warnCount} avisos)",
            errCount > 0 ? "Clique para corrigir automaticamente." : "Arquivo pronto para validação ou divisão mensal.",
            () => NavigateTo(NavSection.CorrecaoEExportacao)));

        var actionsCard = CreateCardShell("Relatório para o Cliente", VectorIcons.DocumentText, null);
        var actionsBody = (StackPanel)actionsCard.Child!;
        actionsBody.Spacing = 7;
        actionsBody.Children.Add(CreateQuickActionButton("Relatório Completo (Entradas x Saídas)", VectorIcons.DocumentText, () => OpenReportPreview(ReportMode.Completo)));
        actionsBody.Children.Add(CreateQuickActionButton("Somente Notas Não Lançadas", VectorIcons.Warning, () => OpenReportPreview(ReportMode.SomenteNotasNaoLancadas)));
        if (_scoped.TopSellingProducts.Count > 0)
            actionsBody.Children.Add(CreateQuickActionButton("Ranking Produtos Mais Vendidos", VectorIcons.ChartBar, () => OpenReportPreview(ReportMode.ProdutosMaisVendidos)));
        if (_scoped.XmlComprasOmissas > 0)
            actionsBody.Children.Add(CreatePrimaryButton($"Lançar {_scoped.XmlComprasOmissas} Faltante(s) no Sintegra", VectorIcons.CheckBadge, async () => await OpenInjectMissingDialogAsync(null)));

        if (compact)
        {
            var compactRight = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
            Grid.SetColumn(alertsCard, 0);
            Grid.SetColumn(actionsCard, 2);
            compactRight.Children.Add(alertsCard);
            compactRight.Children.Add(actionsCard);
            Grid.SetRow(compactRight, 2);
            lowerGrid.Children.Add(compactRight);
        }
        else
        {
            var rightCol = new StackPanel { Spacing = 12 };
            rightCol.Children.Add(alertsCard);
            rightCol.Children.Add(actionsCard);
            Grid.SetColumn(rightCol, 2);
            lowerGrid.Children.Add(rightCol);
        }

        root.Children.Add(lowerGrid);
        return root;
    }

    private Control BuildMonthlyComparisonTable(IReadOnlyList<MonthlyBalanceRow> rows)
    {
        var wrap = new Border
        {
            BorderBrush = BrushBorderLight,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true
        };

        var stack = new StackPanel();
        string cols = "68,0.92*,0.82*,0.98*,0.98*,1.02*,1.22*";

        var hdrGrid = new Grid { ColumnDefinitions = new ColumnDefinitions(cols), Background = BrushHeaderBg };
        AddHeaderCell(hdrGrid, 0, "Mês", TextAlignment.Left);
        AddHeaderCell(hdrGrid, 1, "Compras", TextAlignment.Right);
        AddHeaderCell(hdrGrid, 2, "Outras Entr.", TextAlignment.Right);
        AddHeaderCell(hdrGrid, 3, "Total Entradas", TextAlignment.Right);
        AddHeaderCell(hdrGrid, 4, "Total Saídas", TextAlignment.Right);
        AddHeaderCell(hdrGrid, 5, "Saídas - Entradas", TextAlignment.Right);
        AddHeaderCell(hdrGrid, 6, "Conferência XML", TextAlignment.Right);
        stack.Children.Add(hdrGrid);

        if (rows.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Sem movimentação mensal para o período selecionado.",
                Margin = new Thickness(12),
                Foreground = BrushMuted,
                FontSize = 12
            });
            wrap.Child = stack;
            return wrap;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var rowGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(cols),
                Background = i % 2 == 0 ? Brushes.White : Brush.Parse("#F8FAFC"),
                Cursor = HandCursor
            };

            AddBodyCellFast(rowGrid, 0, r.MonthLabel, TextAlignment.Left, FontWeight.SemiBold, BrushTextDark);
            AddBodyCellFast(rowGrid, 1, $"R$ {TaxIdValidator.FormatCentsBrl(r.ComprasEntradasCents)}", TextAlignment.Right, FontWeight.Normal, BrushTextBody);
            AddBodyCellFast(rowGrid, 2, $"R$ {TaxIdValidator.FormatCentsBrl(r.OutrasEntradasCents)}", TextAlignment.Right, FontWeight.Normal, BrushMuted);
            AddBodyCellFast(rowGrid, 3, $"R$ {TaxIdValidator.FormatCentsBrl(r.TotalEntradasCents)}", TextAlignment.Right, FontWeight.SemiBold, BrushTextDark);
            AddBodyCellFast(rowGrid, 4, $"R$ {TaxIdValidator.FormatCentsBrl(r.TotalSaidasCents)}", TextAlignment.Right, FontWeight.SemiBold, BrushTextDark);
            AddBodyCellFast(rowGrid, 5, TaxIdValidator.FormatSignedCentsBrl(r.SaldoLiquidoCents), TextAlignment.Right, FontWeight.SemiBold, r.SaldoLiquidoCents >= 0 ? BrushGreen : BrushRed);

            string xmlTxt = "Sem XML";
            IBrush xmlBrush = BrushMuted;
            if (_audit is not null)
            {
                if (_scoped.XmlCoverageByMonth.TryGetValue(r.YearMonth, out var cov) && cov.XmlComprasOmissas > 0)
                {
                    xmlTxt = $"{cov.XmlComprasOmissas} falta (R$ {TaxIdValidator.FormatCentsBrl(cov.XmlComprasOmissasCents)})";
                    xmlBrush = BrushRed;
                }
                else
                {
                    xmlTxt = "Todas lançadas";
                    xmlBrush = BrushGreen;
                }
            }
            AddBodyCellFast(rowGrid, 6, xmlTxt, TextAlignment.Right, FontWeight.SemiBold, xmlBrush);

            string ymClicked = r.YearMonth;
            rowGrid.Tapped += (_, _) => SelectPeriodInCombo(_selectedYearMonth == ymClicked ? "ALL" : ymClicked);

            stack.Children.Add(new Border
            {
                BorderBrush = BrushBorderLight,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = rowGrid
            });
        }

        wrap.Child = stack;
        return wrap;
    }

    // ==================== 2. NOTAS FALTANTES (XML) + INJECTOR ====================

    private Control BuildMissingInvoicesView()
    {
        var root = new StackPanel { Spacing = 12 };

        var topBar = new WrapPanel { Orientation = Orientation.Horizontal };
        var btnLoadXml = CreateQuickActionButton("Carregar XML / Pasta...", VectorIcons.UploadZip, null);
        var missXmlFlyout = BuildXmlActionsFlyout();
        btnLoadXml.Click += (_, _) => missXmlFlyout.ShowAt(btnLoadXml);
        btnLoadXml.HorizontalAlignment = HorizontalAlignment.Left;
        btnLoadXml.Margin = new Thickness(0, 2, 10, 2);
        topBar.Children.Add(btnLoadXml);

        if (_audit is not null && _scoped.XmlComprasOmissas > 0)
        {
            var btnInjectAll = CreatePrimaryButton($"Lançar Todas as Faltantes ({_scoped.XmlComprasOmissas})", VectorIcons.CheckBadge, async () => await OpenInjectMissingDialogAsync(null));
            btnInjectAll.Margin = new Thickness(0, 2, 10, 2);
            topBar.Children.Add(btnInjectAll);
        }

        if (_audit is not null && _loadedXmlDocs.Count > 0)
        {
            var btnReprocess = CreateQuickActionButton("Reprocessar XMLs (Datas)", VectorIcons.Wrench, async () => await OpenReprocessXmlDialogAsync());
            btnReprocess.HorizontalAlignment = HorizontalAlignment.Left;
            btnReprocess.Margin = new Thickness(0, 2, 10, 2);
            topBar.Children.Add(btnReprocess);
        }

        var btnExportMissPdf = CreateQuickActionButton("Relatório de Notas Não Lançadas", VectorIcons.DocumentText, () => OpenReportPreview(ReportMode.SomenteNotasNaoLancadas));
        btnExportMissPdf.HorizontalAlignment = HorizontalAlignment.Left;
        btnExportMissPdf.Margin = new Thickness(0, 2, 0, 2);
        topBar.Children.Add(btnExportMissPdf);
        root.Children.Add(topBar);

        if (_audit is null)
        {
            var empty = CreateCardShell("Confronto com XMLs de Compra", VectorIcons.UploadZip, null);
            ((StackPanel)empty.Child!).Children.Add(new TextBlock
            {
                Text = "Carregue o pacote ZIP ou a pasta com os arquivos XML das notas fiscais para listar exatamente quais notas não foram lançadas no Sintegra, em quais meses elas ocorreram, e lançá-las diretamente no arquivo.",
                FontSize = 12.5,
                Foreground = BrushTextBody,
                TextWrapping = TextWrapping.Wrap
            });
            root.Children.Add(empty);
            return root;
        }

        // KPI Summary Row
        var sumGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*,10,*,10,*") };
        var s1 = BuildSmallStatBox("Notas de Compra nos XMLs", _scoped.XmlComprasTotal.ToString(), "#0F172A");
        var s2 = BuildSmallStatBox("Já Lançadas no Sintegra", _scoped.XmlComprasLancadas.ToString(), "#15803D");
        var s3 = BuildSmallStatBox("Não Lançadas (Faltantes)", _scoped.XmlComprasOmissas.ToString(), _scoped.XmlComprasOmissas > 0 ? "#B91C1C" : "#15803D");
        var s4 = BuildSmallStatBox("Valor Total Faltante", $"R$ {TaxIdValidator.FormatCentsBrl(_scoped.XmlComprasOmissasCents)}", _scoped.XmlComprasOmissasCents > 0 ? "#B91C1C" : "#15803D");
        Grid.SetColumn(s1, 0);
        Grid.SetColumn(s2, 2);
        Grid.SetColumn(s3, 4);
        Grid.SetColumn(s4, 6);
        sumGrid.Children.Add(s1);
        sumGrid.Children.Add(s2);
        sumGrid.Children.Add(s3);
        sumGrid.Children.Add(s4);
        root.Children.Add(sumGrid);

        // Monthly breakdown pills showing exact months with missing invoices
        var gapsByMonth = _audit.MonthlyCoverage.Where(m => m.XmlComprasOmissas > 0).ToList();
        if (gapsByMonth.Count > 0)
        {
            var monthCard = CreateCardShell("Meses com Notas Faltantes (clique no mês para filtrar)", VectorIcons.Warning, null);
            var wp = new WrapPanel { Margin = new Thickness(0, 2) };
            foreach (var mg in gapsByMonth)
            {
                string ym = mg.YearMonth;
                wp.Children.Add(CreateFilterChip(
                    $"{mg.MonthLabel}: {mg.XmlComprasOmissas} falta(s) - R$ {TaxIdValidator.FormatCentsBrl(mg.XmlComprasOmissasCents)}",
                    _selectedYearMonth == ym,
                    () => SelectPeriodInCombo(_selectedYearMonth == ym ? "ALL" : ym)));
            }
            ((StackPanel)monthCard.Child!).Children.Add(wp);
            root.Children.Add(monthCard);
        }

        // Filter Chips
        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        filterRow.Children.Add(CreateFilterChip($"Somente Não Lançadas ({_scoped.XmlComprasOmissas})", _auditFilter == "MISSING", () => { _auditFilter = "MISSING"; RenderActiveSection(); }));
        filterRow.Children.Add(CreateFilterChip($"Todas as Notas XML ({_scoped.XmlDocumentRows.Count})", _auditFilter == "ALL", () => { _auditFilter = "ALL"; RenderActiveSection(); }));
        root.Children.Add(filterRow);

        var rows = _auditFilter == "MISSING"
            ? _scoped.XmlDocumentRows.Where(r => r.IsCriticalGap).ToList()
            : _scoped.XmlDocumentRows.ToList();

        var listCard = CreateCardShell(
            _auditFilter == "MISSING"
                ? $"Notas Fiscais de Compra Não Lançadas ({rows.Count})"
                : $"Todos os Documentos Analisados ({rows.Count})",
            VectorIcons.DocumentText,
            new TextBlock
            {
                Text = "Busca automática em todo o Sintegra | Clique na linha ou em Detalhes",
                FontSize = 11,
                Foreground = BrushMuted,
                VerticalAlignment = VerticalAlignment.Center
            });
        var listStack = (StackPanel)listCard.Child!;

        var cols = new[]
        {
            new GridLength(64), new GridLength(80), new GridLength(76),
            new GridLength(1.3, GridUnitType.Star), new GridLength(0.95, GridUnitType.Star),
            new GridLength(0.85, GridUnitType.Star), new GridLength(168)
        };

        var table = rows.Count == 0
            ? BuildEmptyTableWithHeader(cols, hdr =>
            {
                AddHeaderCell(hdr, 0, "Mês", TextAlignment.Left);
                AddHeaderCell(hdr, 1, "Emissão", TextAlignment.Left);
                AddHeaderCell(hdr, 2, "NF / Série", TextAlignment.Left);
                AddHeaderCell(hdr, 3, "Fornecedor", TextAlignment.Left);
                AddHeaderCell(hdr, 4, "Status / Obs.", TextAlignment.Left);
                AddHeaderCell(hdr, 5, "Valor XML", TextAlignment.Right);
                AddHeaderCell(hdr, 6, "Ações", TextAlignment.Center);
            }, _auditFilter == "MISSING"
                ? "Excelente! Nenhuma nota fiscal de compra faltante neste período."
                : "Nenhum documento XML neste filtro.")
            : UiTableHelpers.WrapWithHeader(cols, hdr =>
            {
                AddHeaderCell(hdr, 0, "Mês", TextAlignment.Left);
                AddHeaderCell(hdr, 1, "Emissão", TextAlignment.Left);
                AddHeaderCell(hdr, 2, "NF / Série", TextAlignment.Left);
                AddHeaderCell(hdr, 3, "Fornecedor", TextAlignment.Left);
                AddHeaderCell(hdr, 4, "Status / Obs.", TextAlignment.Left);
                AddHeaderCell(hdr, 5, "Valor XML", TextAlignment.Right);
                AddHeaderCell(hdr, 6, "Ações", TextAlignment.Center);
            }, UiTableHelpers.CreateRecycledListBox<XmlAuditDocumentRow>(
                rows,
                cols,
                (g, slots, d) =>
                {
                    g.Background = d.IsCriticalGap
                        ? BrushErrorBg
                        : (d.Notes.Contains("mês ", StringComparison.OrdinalIgnoreCase) ? Brush.Parse("#EFF6FF") : Brushes.Transparent);

                    UiTableHelpers.SetCellSlot(g, slots, 0, TaxIdValidator.FormatMonthLabel(d.YearMonth), TextAlignment.Left, FontWeight.SemiBold, BrushTextDark);
                    UiTableHelpers.SetCellSlot(g, slots, 1, d.DataBr, TextAlignment.Left, FontWeight.Normal, BrushTextBody);
                    UiTableHelpers.SetCellSlot(g, slots, 2, $"{d.Numero}/{d.Serie}", TextAlignment.Left, FontWeight.SemiBold, BrushTextDark);
                    UiTableHelpers.SetCellSlot(g, slots, 3, d.EmitNome, TextAlignment.Left, FontWeight.Normal, BrushTextBody);

                    string statusDisplay = d.Notes.Contains("mês ", StringComparison.OrdinalIgnoreCase)
                        ? $"{d.MatchStatus} ({d.Notes})"
                        : d.MatchStatus;
                    IBrush statusBrush = d.IsCriticalGap
                        ? BrushRed
                        : (d.Notes.Contains("mês ", StringComparison.OrdinalIgnoreCase) ? BrushBlueDark : BrushGreen);
                    UiTableHelpers.SetCellSlot(g, slots, 4, statusDisplay, TextAlignment.Left, FontWeight.SemiBold, statusBrush);
                    UiTableHelpers.SetCellSlot(g, slots, 5, $"R$ {TaxIdValidator.FormatCentsBrl(d.XmlValorCents)}", TextAlignment.Right, FontWeight.SemiBold, BrushTextDark);

                    if (slots[6] is StackPanel act && act.Children.Count >= 2 && act.Children[1] is Button bOne)
                        bOne.IsVisible = d.IsCriticalGap;
                },
                listHeight: 400,
                initRowSlots: (g, slots) =>
                {
                    var actRow = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var btnDetails = new Button { Content = "Detalhes" };
                    btnDetails.Classes.Add("row-action");
                    btnDetails.Click += async (_, _) =>
                    {
                        if (g.DataContext is XmlAuditDocumentRow cur)
                            await OpenXmlInvoiceDetailsAsync(cur);
                    };
                    actRow.Children.Add(btnDetails);

                    var btnOne = new Button { Content = "Lançar", Padding = new Thickness(9, 3), FontSize = 11, FontWeight = FontWeight.SemiBold };
                    btnOne.Classes.Add("primary");
                    btnOne.Click += async (_, _) =>
                    {
                        if (g.DataContext is XmlAuditDocumentRow cur)
                            await OpenInjectMissingDialogAsync(cur);
                    };
                    actRow.Children.Add(btnOne);

                    Grid.SetColumn(actRow, 6);
                    slots[6] = actRow;
                    g.Children.Add(actRow);
                },
                onActivateRow: d => _ = OpenXmlInvoiceDetailsAsync(d)));

        listStack.Children.Add(table);
        root.Children.Add(listCard);
        return root;
    }

    // ==================== 3. NOTAS FISCAIS (CLICK -> INVOICE DETAILS) ====================

    private Control BuildDocumentsView()
    {
        var root = new StackPanel { Spacing = 10 };

        static string NormDocKey(string cnpj, string mod, string ser, string num) =>
            $"{cnpj.Trim()}|{mod.Trim()}|{ser.Trim().TrimStart('0')}|{num.Trim().TrimStart('0')}";

        var entradaDocs = _scoped.Reg50Docs.Where(r => TaxIdValidator.IsEntradaCfop(r.Cfop)).ToList();
        var saida50Docs = _scoped.Reg50Docs.Where(r => !TaxIdValidator.IsEntradaCfop(r.Cfop)).ToList();

        // Pre-index item counts O(1) so scrolling stays instant across large annual files
        var reg54CountMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var it in _doc.Reg54)
        {
            string k = NormDocKey(it.Cnpj, it.Modelo, it.Serie, it.Numero);
            reg54CountMap[k] = reg54CountMap.TryGetValue(k, out int cur) ? cur + 1 : 1;
        }

        var reg61RCountMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var r61r in _doc.Reg61R)
            reg61RCountMap[r61r.MesAno] = reg61RCountMap.TryGetValue(r61r.MesAno, out int cur) ? cur + 1 : 1;

        var controlsRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var txtSearch = new TextBox
        {
            Watermark = "Buscar por número da NF, CNPJ, CFOP...",
            Text = _docSearchFilter,
            Width = 285,
            FontSize = 12,
            Margin = new Thickness(0, 0, 8, 4)
        };

        var lblCount = new TextBlock
        {
            FontSize = 11.5,
            Foreground = BrushMuted,
            VerticalAlignment = VerticalAlignment.Center
        };

        controlsRow.Children.Add(txtSearch);
        controlsRow.Children.Add(CreateFilterChip($"Entradas NF-e ({entradaDocs.Count})", _docRegFilter is not ("SAI50" or "61"), () => { _docRegFilter = "ENT"; RenderActiveSection(); }));
        controlsRow.Children.Add(CreateFilterChip($"Saídas NF-e ({saida50Docs.Count})", _docRegFilter == "SAI50", () => { _docRegFilter = "SAI50"; RenderActiveSection(); }));
        controlsRow.Children.Add(CreateFilterChip($"Cupons NFC-e Reg. 61 ({_scoped.Reg61Docs.Count})", _docRegFilter == "61", () => { _docRegFilter = "61"; RenderActiveSection(); }));
        root.Children.Add(controlsRow);

        if (_docRegFilter == "61")
        {
            List<Reg61> Filter61() => string.IsNullOrWhiteSpace(_docSearchFilter)
                ? _scoped.Reg61Docs.ToList()
                : _scoped.Reg61Docs.Where(r =>
                    r.NumInicialOrdem.Contains(_docSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    r.NumFinalOrdem.Contains(_docSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    TaxIdValidator.FormatDateBr(r.DataEmissao).Contains(_docSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    r.DataEmissao.Contains(_docSearchFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            var initial61 = Filter61();
            lblCount.Text = $"Exibindo {initial61.Count} de {_scoped.Reg61Docs.Count} dia(s) (clique para ver itens do mês)";

            var cols61 = new[]
            {
                new GridLength(50), new GridLength(86), new GridLength(46),
                new GridLength(86), new GridLength(86),
                new GridLength(1, GridUnitType.Star), new GridLength(0.9, GridUnitType.Star), new GridLength(128)
            };

            string emptyMsg61 = "Nenhum cupom fiscal / NFC-e (Registro 61) neste período.";
            Control body61;
            if (initial61.Count == 0)
            {
                body61 = new Border
                {
                    BorderBrush = BrushBorderLight,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(14),
                    Child = new TextBlock { Text = emptyMsg61, FontSize = 12.5, Foreground = BrushMuted, TextWrapping = TextWrapping.Wrap }
                };
            }
            else
            {
                var listBox61 = UiTableHelpers.CreateRecycledListBox<Reg61>(
                    initial61,
                    cols61,
                    (g, slots, r61) =>
                    {
                        string mmYyyy = r61.DataEmissao.Length >= 6 ? $"{r61.DataEmissao[4..6]}{r61.DataEmissao[..4]}" : "";
                        int prodCnt = reg61RCountMap.TryGetValue(mmYyyy, out int mc) ? mc : 0;

                        UiTableHelpers.SetCellSlot(g, slots, 0, r61.Line.ToString(), TextAlignment.Right, FontWeight.Normal, BrushMuted);
                        UiTableHelpers.SetCellSlot(g, slots, 1, TaxIdValidator.FormatDateBr(r61.DataEmissao), TextAlignment.Left, FontWeight.Normal, BrushTextDark);
                        UiTableHelpers.SetCellSlot(g, slots, 2, r61.Modelo, TextAlignment.Left, FontWeight.Normal, BrushTextBody);
                        UiTableHelpers.SetCellSlot(g, slots, 3, r61.NumInicialOrdem.TrimStart('0').PadLeft(1, '0'), TextAlignment.Right, FontWeight.Normal, BrushTextBody);
                        UiTableHelpers.SetCellSlot(g, slots, 4, r61.NumFinalOrdem.TrimStart('0').PadLeft(1, '0'), TextAlignment.Right, FontWeight.Normal, BrushTextBody);
                        UiTableHelpers.SetCellSlot(g, slots, 5, $"R$ {TaxIdValidator.FormatCentsBrl(r61.ValorTotalCents)}", TextAlignment.Right, FontWeight.SemiBold, BrushGreen);
                        UiTableHelpers.SetCellSlot(g, slots, 6, $"R$ {TaxIdValidator.FormatCentsBrl(r61.ValorIcmsCents)}", TextAlignment.Right, FontWeight.Normal, BrushMuted);

                        if (slots[7] is Button btnItens)
                        {
                            btnItens.Content = prodCnt > 0 ? $"Itens mês ({prodCnt})" : "Ver mês";
                            btnItens.Classes.Set("accent", prodCnt > 0);
                        }
                    },
                    listHeight: 455,
                    initRowSlots: (g, slots) =>
                    {
                        var btnItens = new Button
                        {
                            Margin = new Thickness(4, 2),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        btnItens.Classes.Add("row-action");
                        btnItens.Click += (_, _) =>
                        {
                            if (g.DataContext is Reg61 cur)
                                OpenCouponMonthDetails(cur);
                        };
                        Grid.SetColumn(btnItens, 7);
                        slots[7] = btnItens;
                        g.Children.Add(btnItens);
                    },
                    onActivateRow: OpenCouponMonthDetails);

                txtSearch.TextChanged += (_, _) =>
                {
                    string next = txtSearch.Text ?? "";
                    if (string.Equals(next, _docSearchFilter, StringComparison.Ordinal)) return;
                    _docSearchFilter = next;
                    var updated = Filter61();
                    listBox61.ItemsSource = updated;
                    lblCount.Text = $"Exibindo {updated.Count} de {_scoped.Reg61Docs.Count} dia(s) (clique ou Enter para ver itens do mês)";
                };

                body61 = UiTableHelpers.WrapWithHeader(cols61, hdr =>
                {
                    AddHeaderCell(hdr, 0, "Linha", TextAlignment.Right);
                    AddHeaderCell(hdr, 1, "Data", TextAlignment.Left);
                    AddHeaderCell(hdr, 2, "Mod", TextAlignment.Left);
                    AddHeaderCell(hdr, 3, "Cupom Ini.", TextAlignment.Right);
                    AddHeaderCell(hdr, 4, "Cupom Fim", TextAlignment.Right);
                    AddHeaderCell(hdr, 5, "Valor Total", TextAlignment.Right);
                    AddHeaderCell(hdr, 6, "Valor ICMS", TextAlignment.Right);
                    AddHeaderCell(hdr, 7, "Itens (Reg. 61R)", TextAlignment.Center);
                }, listBox61);
            }

            var card61 = CreateCardShell("Cupons Fiscais / NFC-e (Resumo Diário - Registro 61)", VectorIcons.DocumentText, lblCount);
            ((StackPanel)card61.Child!).Children.Add(body61);
            root.Children.Add(card61);
            return root;
        }

        bool isSaidaView = _docRegFilter == "SAI50";
        var source50 = isSaidaView ? saida50Docs : entradaDocs;

        List<Reg50> Filter50() => string.IsNullOrWhiteSpace(_docSearchFilter)
            ? source50
            : source50.Where(r =>
                r.Numero.Contains(_docSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                r.Cnpj.Contains(_docSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                TaxIdValidator.FormatDateBr(r.DataEmissao).Contains(_docSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                r.Cfop.Contains(_docSearchFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        var initial50 = Filter50();
        lblCount.Text = $"Exibindo {initial50.Count} de {source50.Count} (clique na nota para ver os itens)";

        var cols50 = new[]
        {
            new GridLength(82), new GridLength(1.15, GridUnitType.Star),
            new GridLength(40), new GridLength(72), new GridLength(52), new GridLength(36),
            new GridLength(0.95, GridUnitType.Star), new GridLength(0.85, GridUnitType.Star), new GridLength(36), new GridLength(104)
        };

        Control body50;
        if (source50.Count == 0)
        {
            string emptyMsg = isSaidaView
                ? "Nenhuma NF-e de saída (modelo 55/01 com CFOP 5xxx/6xxx/7xxx) foi lançada neste período. Se a empresa vendeu apenas para consumidor final, as saídas ficam em 'Cupons NFC-e Reg. 61'."
                : "Nenhuma NF-e de entrada (CFOP 1xxx/2xxx/3xxx) encontrada neste período.";
            body50 = new Border
            {
                BorderBrush = BrushBorderLight,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(14),
                Child = new TextBlock { Text = emptyMsg, FontSize = 12.5, Foreground = BrushMuted, TextWrapping = TextWrapping.Wrap }
            };
        }
        else
        {
            var listBox50 = UiTableHelpers.CreateRecycledListBox<Reg50>(
                initial50,
                cols50,
                (g, slots, r50) =>
                {
                    int itemCnt = reg54CountMap.TryGetValue(NormDocKey(r50.Cnpj, r50.Modelo, r50.Serie, r50.Numero), out int cnt) ? cnt : 0;

                    UiTableHelpers.SetCellSlot(g, slots, 0, TaxIdValidator.FormatDateBr(r50.DataEmissao), TextAlignment.Left, FontWeight.Normal, BrushTextDark);
                    UiTableHelpers.SetCellSlot(g, slots, 1, TaxIdValidator.FormatCnpj(r50.Cnpj), TextAlignment.Left, FontWeight.Normal, BrushTextBody);
                    UiTableHelpers.SetCellSlot(g, slots, 2, r50.Modelo, TextAlignment.Left, FontWeight.Normal, BrushMuted);
                    UiTableHelpers.SetCellSlot(g, slots, 3, r50.Numero.TrimStart('0').PadLeft(1, '0'), TextAlignment.Left, FontWeight.SemiBold, BrushTextDark);
                    UiTableHelpers.SetCellSlot(g, slots, 4, r50.Cfop, TextAlignment.Left, FontWeight.SemiBold, isSaidaView ? BrushGreen : BrushBlueDark);
                    UiTableHelpers.SetCellSlot(g, slots, 5, r50.Uf, TextAlignment.Left, FontWeight.Normal, BrushMuted);
                    UiTableHelpers.SetCellSlot(g, slots, 6, $"R$ {TaxIdValidator.FormatCentsBrl(r50.ValorTotalCents)}", TextAlignment.Right, FontWeight.SemiBold, BrushTextDark);
                    UiTableHelpers.SetCellSlot(g, slots, 7, $"R$ {TaxIdValidator.FormatCentsBrl(r50.ValorIcmsCents)}", TextAlignment.Right, FontWeight.Normal, BrushMuted);
                    UiTableHelpers.SetCellSlot(g, slots, 8, r50.Situacao, TextAlignment.Left, FontWeight.SemiBold, r50.Situacao == "S" ? BrushRed : BrushTextBody);

                    if (slots[9] is Button btnItens)
                    {
                        btnItens.Content = itemCnt > 0 ? $"Itens ({itemCnt})" : "Resumo";
                        btnItens.Classes.Set("accent", itemCnt > 0);
                    }
                },
                listHeight: 455,
                initRowSlots: (g, slots) =>
                {
                    var btnItens = new Button
                    {
                        Margin = new Thickness(4, 2),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    btnItens.Classes.Add("row-action");
                    btnItens.Click += (_, _) =>
                    {
                        if (g.DataContext is Reg50 cur)
                            OpenInvoiceDetails(cur);
                    };
                    Grid.SetColumn(btnItens, 9);
                    slots[9] = btnItens;
                    g.Children.Add(btnItens);
                },
                onActivateRow: OpenInvoiceDetails);

            txtSearch.TextChanged += (_, _) =>
            {
                string next = txtSearch.Text ?? "";
                if (string.Equals(next, _docSearchFilter, StringComparison.Ordinal)) return;
                _docSearchFilter = next;
                var updated = Filter50();
                listBox50.ItemsSource = updated;
                lblCount.Text = $"Exibindo {updated.Count} de {source50.Count} (clique ou Enter para ver os itens)";
            };

            body50 = UiTableHelpers.WrapWithHeader(cols50, hdr =>
            {
                AddHeaderCell(hdr, 0, "Data", TextAlignment.Left);
                AddHeaderCell(hdr, 1, isSaidaView ? "CNPJ Destinatário" : "CNPJ Fornecedor", TextAlignment.Left);
                AddHeaderCell(hdr, 2, "Mod", TextAlignment.Left);
                AddHeaderCell(hdr, 3, "Número", TextAlignment.Left);
                AddHeaderCell(hdr, 4, "CFOP", TextAlignment.Left);
                AddHeaderCell(hdr, 5, "UF", TextAlignment.Left);
                AddHeaderCell(hdr, 6, "Valor Total", TextAlignment.Right);
                AddHeaderCell(hdr, 7, "ICMS", TextAlignment.Right);
                AddHeaderCell(hdr, 8, "Sit.", TextAlignment.Left);
                AddHeaderCell(hdr, 9, "Detalhes", TextAlignment.Center);
            }, listBox50);
        }

        string cardTitle = isSaidaView
            ? "Notas Fiscais de Saída NF-e (Registro 50)"
            : "Notas Fiscais de Entrada NF-e (Registro 50)";
        var card50 = CreateCardShell(cardTitle, VectorIcons.DocumentText, lblCount);
        ((StackPanel)card50.Child!).Children.Add(body50);
        root.Children.Add(card50);
        return root;
    }

    // ==================== 4. CFOP E PRODUTOS MAIS VENDIDOS ====================

    private Control BuildCfopAndProductsView()
    {
        var root = new StackPanel { Spacing = 12 };

        // Top: Top-Selling Products Ranking (Reg 61R / Reg 54)
        var prodCard = CreateCardShell($"Produtos Mais Vendidos / Movimentados - {_scoped.PeriodLabel} ({_scoped.TopSellingProducts.Count} item(ns))", VectorIcons.ChartBar, null);
        var prodStack = (StackPanel)prodCard.Child!;
        var pCols = new[]
        {
            new GridLength(42), new GridLength(95), new GridLength(1.8, GridUnitType.Star),
            new GridLength(48), new GridLength(82), new GridLength(108), new GridLength(68)
        };
        var topProducts = _scoped.TopSellingProducts.ToList();
        var prodRank = new Dictionary<TopProductRow, int>(topProducts.Count);
        for (int i = 0; i < topProducts.Count; i++)
            prodRank[topProducts[i]] = i + 1;

        prodStack.Children.Add(topProducts.Count == 0
            ? BuildEmptyTableWithHeader(pCols, hdr =>
            {
                AddHeaderCell(hdr, 0, "#", TextAlignment.Left);
                AddHeaderCell(hdr, 1, "Código", TextAlignment.Left);
                AddHeaderCell(hdr, 2, "Produto (Registro 75)", TextAlignment.Left);
                AddHeaderCell(hdr, 3, "Un.", TextAlignment.Left);
                AddHeaderCell(hdr, 4, "Qtd. Vendida", TextAlignment.Right);
                AddHeaderCell(hdr, 5, "Valor Total", TextAlignment.Right);
                AddHeaderCell(hdr, 6, "% Vendas", TextAlignment.Right);
            }, "Nenhum resumo mensal de itens vendidos (Reg. 61R ou Reg. 54 de saída) encontrado neste arquivo.")
            : UiTableHelpers.WrapWithHeader(pCols, hdr =>
            {
                AddHeaderCell(hdr, 0, "#", TextAlignment.Left);
                AddHeaderCell(hdr, 1, "Código", TextAlignment.Left);
                AddHeaderCell(hdr, 2, "Produto (Registro 75)", TextAlignment.Left);
                AddHeaderCell(hdr, 3, "Un.", TextAlignment.Left);
                AddHeaderCell(hdr, 4, "Qtd. Vendida", TextAlignment.Right);
                AddHeaderCell(hdr, 5, "Valor Total", TextAlignment.Right);
                AddHeaderCell(hdr, 6, "% Vendas", TextAlignment.Right);
            }, UiTableHelpers.CreateRecycledListBox<TopProductRow>(
                topProducts,
                pCols,
                (g, slots, p) =>
                {
                    int idx = prodRank.TryGetValue(p, out int r) ? r : 0;
                    UiTableHelpers.SetCellSlot(g, slots, 0, $"{idx}.", TextAlignment.Left, FontWeight.SemiBold, BrushMuted);
                    UiTableHelpers.SetCellSlot(g, slots, 1, p.CodProduto, TextAlignment.Left, FontWeight.Normal, BrushTextBody);
                    UiTableHelpers.SetCellSlot(g, slots, 2, p.Descricao, TextAlignment.Left, FontWeight.SemiBold, BrushTextDark);
                    UiTableHelpers.SetCellSlot(g, slots, 3, p.Unidade, TextAlignment.Left, FontWeight.Normal, BrushMuted);
                    UiTableHelpers.SetCellSlot(g, slots, 4, TaxIdValidator.FormatMilliBr(p.QuantidadeMilli), TextAlignment.Right, FontWeight.Normal, BrushTextBody);
                    UiTableHelpers.SetCellSlot(g, slots, 5, $"R$ {TaxIdValidator.FormatCentsBrl(p.ValorTotalCents)}", TextAlignment.Right, FontWeight.SemiBold, BrushGreen);
                    UiTableHelpers.SetCellSlot(g, slots, 6, $"{p.SharePct:N1}%", TextAlignment.Right, FontWeight.Normal, BrushMuted);
                },
                listHeight: 280)));
        root.Children.Add(prodCard);

        // Bottom: CFOP Summary
        var cfopCard = CreateCardShell($"Resumo por CFOP - {_scoped.PeriodLabel}", VectorIcons.Scale, null);
        var cfopStack = (StackPanel)cfopCard.Child!;
        var tbl = new Border { BorderBrush = BrushBorderLight, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
        var rows = new StackPanel();
        string cols = "65,1.4*,60,1*,1*,1*,1*,1*";
        var hdrCfop = new Grid { ColumnDefinitions = new ColumnDefinitions(cols), Background = BrushHeaderBg };
        AddHeaderCell(hdrCfop, 0, "CFOP", TextAlignment.Left);
        AddHeaderCell(hdrCfop, 1, "Natureza da Operação", TextAlignment.Left);
        AddHeaderCell(hdrCfop, 2, "Notas", TextAlignment.Right);
        AddHeaderCell(hdrCfop, 3, "Vlr. Contábil", TextAlignment.Right);
        AddHeaderCell(hdrCfop, 4, "Base ICMS", TextAlignment.Right);
        AddHeaderCell(hdrCfop, 5, "Valor ICMS", TextAlignment.Right);
        AddHeaderCell(hdrCfop, 6, "Isentas", TextAlignment.Right);
        AddHeaderCell(hdrCfop, 7, "Outras", TextAlignment.Right);
        rows.Children.Add(hdrCfop);

        int iCfop = 0;
        foreach (var c in _scoped.CfopRows)
        {
            var g = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(cols),
                Background = iCfop++ % 2 == 0 ? Brushes.White : Brush.Parse("#F8FAFC")
            };
            AddBodyCellFast(g, 0, c.Cfop, TextAlignment.Left, FontWeight.Bold, TaxIdValidator.IsEntradaCfop(c.Cfop) ? BrushBlueDark : BrushGreen);
            AddBodyCellFast(g, 1, c.Nature, TextAlignment.Left, FontWeight.Normal, BrushTextBody);
            AddBodyCellFast(g, 2, c.DocumentCount.ToString(), TextAlignment.Right, FontWeight.Normal, BrushTextBody);
            AddBodyCellFast(g, 3, $"R$ {TaxIdValidator.FormatCentsBrl(c.ValorContabilCents)}", TextAlignment.Right, FontWeight.SemiBold, BrushTextDark);
            AddBodyCellFast(g, 4, $"R$ {TaxIdValidator.FormatCentsBrl(c.BaseIcmsCents)}", TextAlignment.Right, FontWeight.Normal, BrushMuted);
            AddBodyCellFast(g, 5, $"R$ {TaxIdValidator.FormatCentsBrl(c.ValorIcmsCents)}", TextAlignment.Right, FontWeight.Normal, BrushMuted);
            AddBodyCellFast(g, 6, $"R$ {TaxIdValidator.FormatCentsBrl(c.IsentaCents)}", TextAlignment.Right, FontWeight.Normal, BrushMuted);
            AddBodyCellFast(g, 7, $"R$ {TaxIdValidator.FormatCentsBrl(c.OutrasCents)}", TextAlignment.Right, FontWeight.Normal, BrushMuted);
            rows.Children.Add(new Border { BorderBrush = BrushBorderLight, BorderThickness = new Thickness(0, 1, 0, 0), Child = g });
        }
        tbl.Child = rows;
        cfopStack.Children.Add(tbl);
        root.Children.Add(cfopCard);

        return root;
    }

    // ==================== 5. CORRIGIR E EXPORTAR ====================

    private Control BuildFixAndExportView()
    {
        var root = new StackPanel { Spacing = 12 };

        var fixCard = CreateCardShell("Correção Automática, Inclusão de Faltantes e Exportação", VectorIcons.Wrench, null);
        var stack = (StackPanel)fixCard.Child!;
        stack.Spacing = 10;

        stack.Children.Add(new TextBlock
        {
            Text = "Corrige quebras CRLF, alinhamento de 126 colunas, notas canceladas, produtos Reg. 75 (incluindo Reg. 61R), ordenação de itens Reg. 54 e totalizadores do Reg. 90.",
            FontSize = 12,
            Foreground = BrushTextBody,
            TextWrapping = TextWrapping.Wrap
        });

        var btns = new WrapPanel { Orientation = Orientation.Horizontal };
        var btnFixNow = CreatePrimaryButton("Corrigir Erros Agora (Auto-Fix)", VectorIcons.Wrench, async () => await RunQuickFixAndRefreshAsync());
        btnFixNow.Margin = new Thickness(0, 2, 10, 2);
        btns.Children.Add(btnFixNow);
        if (_scoped.XmlComprasOmissas > 0)
        {
            var btnLaunchFix = CreatePrimaryButton($"Lançar {_scoped.XmlComprasOmissas} XML(s) Faltantes", VectorIcons.CheckBadge, async () => await OpenInjectMissingDialogAsync(null));
            btnLaunchFix.Margin = new Thickness(0, 2, 10, 2);
            btns.Children.Add(btnLaunchFix);
        }

        var btnSave = CreateQuickActionButton("Salvar Sintegra Atualizado (.txt)...", VectorIcons.ExportDoc, async () => await SaveCurrentBytesDialogAsync());
        btnSave.HorizontalAlignment = HorizontalAlignment.Left;
        btnSave.Margin = new Thickness(0, 2, 10, 2);
        var btnSplit = CreateQuickActionButton("Dividir Anual em Arquivos Mensais...", VectorIcons.Split, async () => await ExportMonthlySplitsDialogAsync());
        btnSplit.HorizontalAlignment = HorizontalAlignment.Left;
        btnSplit.Margin = new Thickness(0, 2, 0, 2);

        btns.Children.Add(btnSave);
        btns.Children.Add(btnSplit);
        stack.Children.Add(btns);

        if (_lastFixReport is { } fx)
        {
            var resBox = new Border
            {
                Background = Brush.Parse("#F0FDF4"),
                BorderBrush = Brush.Parse("#BBF7D0"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 10)
            };
            var resStack = new StackPanel { Spacing = 4 };
            resStack.Children.Add(new TextBlock
            {
                Text = $"Resultado: {fx.BeforeSummary.ErrorCount} erros / {fx.BeforeSummary.WarningCount} avisos  ->  {fx.AfterSummary.ErrorCount} erros / {fx.AfterSummary.WarningCount} avisos",
                FontSize = 12.5,
                FontWeight = FontWeight.Bold,
                Foreground = BrushGreen
            });
            foreach (var af in fx.AppliedFixes)
            {
                resStack.Children.Add(new TextBlock
                {
                    Text = $"[{af.Code}] {af.Description} ({af.AffectedCount} registro(s))",
                    FontSize = 11.5,
                    Foreground = Brush.Parse("#166534")
                });
            }
            resBox.Child = resStack;
            stack.Children.Add(resBox);
        }
        root.Children.Add(fixCard);

        // Diagnostics List
        var diagFilterBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        diagFilterBar.Children.Add(CreateFilterChip($"Todos ({_report.Diagnostics.Count})", _diagFilter == "ALL", () => { _diagFilter = "ALL"; RenderActiveSection(); }));
        diagFilterBar.Children.Add(CreateFilterChip($"Erros ({_report.Summary.ErrorCount})", _diagFilter == "ERR", () => { _diagFilter = "ERR"; RenderActiveSection(); }));
        diagFilterBar.Children.Add(CreateFilterChip($"Avisos ({_report.Summary.WarningCount})", _diagFilter == "WARN", () => { _diagFilter = "WARN"; RenderActiveSection(); }));
        root.Children.Add(diagFilterBar);

        var filtered = _report.Diagnostics
            .Where(d => _diagFilter switch
            {
                "ERR" => d.Severity == Severity.Error,
                "WARN" => d.Severity == Severity.Warning,
                _ => true
            })
            .ToList();

        var diagCard = CreateCardShell($"Diagnósticos Convênio 57/95 ({filtered.Count} - clique para inspecionar linha)", VectorIcons.Warning, null);
        var dCols = new[]
        {
            new GridLength(65), new GridLength(95), new GridLength(55), new GridLength(50),
            new GridLength(1.0, GridUnitType.Star), new GridLength(1.8, GridUnitType.Star), new GridLength(70)
        };
        ((StackPanel)diagCard.Child!).Children.Add(filtered.Count == 0
            ? BuildEmptyTableWithHeader(dCols, hdr =>
            {
                AddHeaderCell(hdr, 0, "Nível", TextAlignment.Left);
                AddHeaderCell(hdr, 1, "Código", TextAlignment.Left);
                AddHeaderCell(hdr, 2, "Linha", TextAlignment.Right);
                AddHeaderCell(hdr, 3, "Reg.", TextAlignment.Left);
                AddHeaderCell(hdr, 4, "Campo", TextAlignment.Left);
                AddHeaderCell(hdr, 5, "Descrição", TextAlignment.Left);
                AddHeaderCell(hdr, 6, "AutoFix", TextAlignment.Center);
            }, "Nenhuma ocorrência neste filtro.")
            : UiTableHelpers.WrapWithHeader(dCols, hdr =>
            {
                AddHeaderCell(hdr, 0, "Nível", TextAlignment.Left);
                AddHeaderCell(hdr, 1, "Código", TextAlignment.Left);
                AddHeaderCell(hdr, 2, "Linha", TextAlignment.Right);
                AddHeaderCell(hdr, 3, "Reg.", TextAlignment.Left);
                AddHeaderCell(hdr, 4, "Campo", TextAlignment.Left);
                AddHeaderCell(hdr, 5, "Descrição", TextAlignment.Left);
                AddHeaderCell(hdr, 6, "AutoFix", TextAlignment.Center);
            }, UiTableHelpers.CreateRecycledListBox<Diagnostic>(
                filtered,
                dCols,
                (g, slots, d) =>
                {
                    IBrush sevBrush = d.Severity switch
                    {
                        Severity.Error => BrushRed,
                        Severity.Warning => BrushAmber,
                        _ => BrushBlueInfo
                    };
                    UiTableHelpers.SetCellSlot(g, slots, 0, d.SeverityLabel, TextAlignment.Left, FontWeight.Bold, sevBrush);
                    UiTableHelpers.SetCellSlot(g, slots, 1, d.Code, TextAlignment.Left, FontWeight.SemiBold, BrushTextBody);
                    UiTableHelpers.SetCellSlot(g, slots, 2, d.Line.ToString(), TextAlignment.Right, FontWeight.Normal, BrushMuted);
                    UiTableHelpers.SetCellSlot(g, slots, 3, d.RecordType, TextAlignment.Left, FontWeight.SemiBold, BrushTextDark);
                    UiTableHelpers.SetCellSlot(g, slots, 4, d.FieldName, TextAlignment.Left, FontWeight.Normal, BrushTextBody);
                    UiTableHelpers.SetCellSlot(g, slots, 5, d.Message, TextAlignment.Left, FontWeight.Normal, BrushTextDark);
                    UiTableHelpers.SetCellSlot(g, slots, 6, d.AutoFixable ? "Sim" : "Não", TextAlignment.Center, FontWeight.SemiBold, d.AutoFixable ? BrushGreen : BrushMuted);
                },
                listHeight: 330,
                onActivateRow: d => InspectLineAt(d.Line, d.ColumnStart, d.ColumnEnd))));

        root.Children.Add(diagCard);
        return root;
    }

    // ==================== 6. EMPRESAS E BANCO SQLITE ====================

    private Control BuildCompanyAndDbView()
    {
        var root = new StackPanel { Spacing = 12 };

        var stats = _db.GetStorageStats();
        var cfgCard = CreateCardShell("Regime Tributário e Limite da Base SQLite", VectorIcons.SettingsGear, null);
        var stack = (StackPanel)cfgCard.Child!;
        stack.Spacing = 10;

        stack.Children.Add(new TextBlock { Text = $"Arquivo SQLite: {stats.DbPath}", FontSize = 12, Foreground = BrushTextBody });
        stack.Children.Add(new TextBlock { Text = $"Espaço em uso: {stats.FormattedSummary}", FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = BrushTextDark });

        var limitRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        limitRow.Children.Add(new TextBlock { Text = "Limite máximo (MB):", FontSize = 12, Foreground = BrushTextBody, VerticalAlignment = VerticalAlignment.Center });
        var numLimit = new NumericUpDown { Minimum = 16, Maximum = 102400, Increment = 128, Value = stats.MaxLimitMb, Width = 140, FormatString = "N0" };
        limitRow.Children.Add(numLimit);
        limitRow.Children.Add(CreateFilterChip("512 MB", stats.MaxLimitMb == 512, () => { numLimit.Value = 512; }));
        limitRow.Children.Add(CreateFilterChip("1 GB", stats.MaxLimitMb == 1024, () => { numLimit.Value = 1024; }));
        limitRow.Children.Add(CreateFilterChip("5 GB", stats.MaxLimitMb == 5120, () => { numLimit.Value = 5120; }));
        stack.Children.Add(limitRow);

        var chkPrune = new CheckBox
        {
            Content = "Remover importações antigas automaticamente ao atingir o limite configurado",
            IsChecked = stats.AutoPrune,
            FontSize = 12
        };
        stack.Children.Add(chkPrune);

        var regRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        regRow.Children.Add(new TextBlock { Text = "Regime Tributário da empresa:", FontSize = 12, Foreground = BrushTextBody, VerticalAlignment = VerticalAlignment.Center });
        var cmbRegime = new ComboBox { Width = 220, FontSize = 12 };
        string currentRegime = _doc.Reg10 is not null ? _db.GetCompanyTaxRegime(_doc.Reg10.Cnpj) : "Simples Nacional";
        foreach (string opt in new[] { "Simples Nacional", "Lucro Presumido", "Lucro Real", "MEI / SIMEI" })
        {
            cmbRegime.Items.Add(opt);
            if (opt == currentRegime) cmbRegime.SelectedItem = opt;
        }
        if (cmbRegime.SelectedIndex < 0) cmbRegime.SelectedIndex = 0;
        regRow.Children.Add(cmbRegime);
        stack.Children.Add(regRow);

        var btnRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var btnSaveCfg = CreatePrimaryButton("Salvar Configurações", VectorIcons.CheckBadge, () =>
        {
            _db.UpdateSettings((long)(numLimit.Value ?? 1024), chkPrune.IsChecked ?? true);
            if (_doc.Reg10 is not null && cmbRegime.SelectedItem is string reg)
                _db.UpdateCompanyTaxRegime(_doc.Reg10.Cnpj, reg);
            UpdateHeaderAndStatusBar();
            RenderActiveSection();
        });
        btnSaveCfg.Margin = new Thickness(0, 2, 10, 2);
        btnRow.Children.Add(btnSaveCfg);
        var btnVacuum = CreateQuickActionButton("Compactar Base (VACUUM)", VectorIcons.Database, () =>
        {
            _db.CompactDatabase();
            UpdateHeaderAndStatusBar();
            RenderActiveSection();
        });
        btnVacuum.HorizontalAlignment = HorizontalAlignment.Left;
        btnVacuum.Margin = new Thickness(0, 2, 10, 2);
        var btnClear = CreateQuickActionButton("Limpar Histórico", VectorIcons.Warning, () =>
        {
            _db.ClearAllHistory();
            UpdateHeaderAndStatusBar();
            RenderActiveSection();
        });
        btnClear.HorizontalAlignment = HorizontalAlignment.Left;
        btnClear.Margin = new Thickness(0, 2, 0, 2);
        btnRow.Children.Add(btnVacuum);
        btnRow.Children.Add(btnClear);
        stack.Children.Add(btnRow);
        root.Children.Add(cfgCard);

        // Company Monthly Timeline History
        var companies = _db.GetCompanies();
        if (string.IsNullOrEmpty(_selectedCompanyCnpj) && companies.Count > 0)
            _selectedCompanyCnpj = companies[0].Cnpj;

        var compCard = CreateCardShell($"Empresas Registradas no SQLite ({companies.Count})", VectorIcons.Building, null);
        var compStack = (StackPanel)compCard.Child!;
        if (companies.Count == 0)
        {
            compStack.Children.Add(new TextBlock { Text = "Nenhuma empresa registrada ainda.", FontSize = 12, Foreground = BrushMuted });
        }
        else
        {
            var pills = new WrapPanel();
            foreach (var c in companies)
            {
                pills.Children.Add(CreateFilterChip($"{c.RazaoSocial} ({TaxIdValidator.FormatCnpj(c.Cnpj)})", c.Cnpj == _selectedCompanyCnpj, () =>
                {
                    _selectedCompanyCnpj = c.Cnpj;
                    RenderActiveSection();
                }));
            }
            compStack.Children.Add(pills);
        }
        root.Children.Add(compCard);

        if (!string.IsNullOrEmpty(_selectedCompanyCnpj))
        {
            var timeline = _db.GetCompanyMonthlyTimeline(_selectedCompanyCnpj);
            var tlCard = CreateCardShell($"Histórico Mensal Salvo ({timeline.Count} competência(s))", VectorIcons.ChartBar, null);
            var tlStack = (StackPanel)tlCard.Child!;
            if (timeline.Count > 0)
            {
                var tbl = new Border { BorderBrush = BrushBorderLight, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
                var rows = new StackPanel();
                string cols = "76,0.95*,0.82*,0.95*,0.95*,1.0*,1.25*";
                var hdr = new Grid { ColumnDefinitions = new ColumnDefinitions(cols), Background = BrushHeaderBg };
                AddHeaderCell(hdr, 0, "Mês", TextAlignment.Left);
                AddHeaderCell(hdr, 1, "Compras", TextAlignment.Right);
                AddHeaderCell(hdr, 2, "Outras", TextAlignment.Right);
                AddHeaderCell(hdr, 3, "Entradas", TextAlignment.Right);
                AddHeaderCell(hdr, 4, "Saídas", TextAlignment.Right);
                AddHeaderCell(hdr, 5, "Saídas - Entradas", TextAlignment.Right);
                AddHeaderCell(hdr, 6, "Arquivo Origem", TextAlignment.Left);
                rows.Children.Add(hdr);

                int idx = 0;
                foreach (var t in timeline)
                {
                    long saldoT = t.TotalSaidasCents - t.TotalEntradasCents;
                    var g = new Grid { ColumnDefinitions = new ColumnDefinitions(cols), Background = idx++ % 2 == 0 ? Brushes.White : Brush.Parse("#F8FAFC") };
                    AddBodyCellFast(g, 0, t.MonthLabel, TextAlignment.Left, FontWeight.SemiBold, BrushTextDark);
                    AddBodyCellFast(g, 1, $"R$ {TaxIdValidator.FormatCentsBrl(t.ComprasCents)}", TextAlignment.Right, FontWeight.Normal, BrushTextBody);
                    AddBodyCellFast(g, 2, $"R$ {TaxIdValidator.FormatCentsBrl(t.OutrasEntradasCents)}", TextAlignment.Right, FontWeight.Normal, BrushMuted);
                    AddBodyCellFast(g, 3, $"R$ {TaxIdValidator.FormatCentsBrl(t.TotalEntradasCents)}", TextAlignment.Right, FontWeight.SemiBold, BrushTextDark);
                    AddBodyCellFast(g, 4, $"R$ {TaxIdValidator.FormatCentsBrl(t.TotalSaidasCents)}", TextAlignment.Right, FontWeight.SemiBold, BrushTextDark);
                    AddBodyCellFast(g, 5, TaxIdValidator.FormatSignedCentsBrl(saldoT), TextAlignment.Right, FontWeight.SemiBold, saldoT >= 0 ? BrushGreen : BrushRed);
                    AddBodyCellFast(g, 6, t.SourceFileName, TextAlignment.Left, FontWeight.Normal, BrushMuted);
                    rows.Children.Add(new Border { BorderBrush = BrushBorderLight, BorderThickness = new Thickness(0, 1, 0, 0), Child = g });
                }
                tbl.Child = rows;
                tlStack.Children.Add(tbl);
            }
            root.Children.Add(tlCard);
        }

        return root;
    }

    // ==================== WORKFLOWS: OPEN, AUDIT, INJECT, FIX, EXPORT ====================

    private async Task PickAndOpenSintegraAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Abrir Arquivo Sintegra (.txt)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Arquivos Sintegra (*.txt)") { Patterns = new[] { "*.txt", "*.TXT" } },
                new FilePickerFileType("Todos os arquivos") { Patterns = new[] { "*.*" } }
            }
        });
        if (files.Count == 0) return;
        string? localPath = files[0].TryGetLocalPath();
        if (!string.IsNullOrEmpty(localPath))
            await LoadSintegraFileAsync(localPath);
    }

    private async Task LoadSintegraFileAsync(string filePath)
    {
        var dlg = new ImportProgressWindow("Importando Sintegra");
        var progress = new Progress<(int Percent, string Status)>(p => dlg.UpdateProgress(p.Percent, p.Status));
        byte[]? bytes = null;
        SintegraDocument? parsedDoc = null;
        ValidationReport? parsedReport = null;
        Exception? failure = null;

        dlg.Opened += async (_, _) =>
        {
            try
            {
                await Task.Run(() =>
                {
                    ((IProgress<(int, string)>)progress).Report((8, $"Lendo arquivo: {Path.GetFileName(filePath)}"));
                    bytes = File.ReadAllBytes(filePath);
                    parsedDoc = SintegraParser.ParseBytes(bytes, progress, dlg.Token);
                    ((IProgress<(int, string)>)progress).Report((92, "Validando regras do Convênio ICMS 57/95..."));
                    parsedReport = SintegraValidator.Validate(parsedDoc);
                    ((IProgress<(int, string)>)progress).Report((100, "Importação concluída."));
                }, dlg.Token);
            }
            catch (OperationCanceledException) { failure = new OperationCanceledException(); }
            catch (Exception ex) { failure = ex; }
            finally { dlg.Close(); }
        };

        await dlg.ShowDialog(this);

        if (failure is OperationCanceledException) return;
        if (failure is not null)
        {
            await ShowInfoDialogAsync("Erro ao abrir Sintegra", failure.Message);
            return;
        }
        if (bytes is null || parsedDoc is null || parsedReport is null) return;

        _hasDocument = true;
        _hasUnsavedChanges = false;
        _currentFilePath = filePath;
        _currentFileName = Path.GetFileName(filePath);
        _currentBytes = bytes;
        _doc = parsedDoc;
        _report = parsedReport;
        _selectedYearMonth = "ALL";
        _lastFixReport = null;
        _selectedCompanyCnpj = _doc.Reg10?.Cnpj;

        // If XMLs were already loaded in memory, automatically re-run audit against the newly loaded Sintegra!
        _audit = _loadedXmlDocs.Count > 0 || _loadedSped is not null
            ? FiscalCrossAuditor.RunCrossAudit(_doc, _loadedXmlDocs, _loadedSped)
            : null;

        try { _db.SetSetting("last_sintegra_path", filePath); } catch { }
        if (_doc.Reg10 is not null)
        {
            try { _db.RecordImport(_doc, _report, _audit, _currentFileName, _lastXmlSourceLabel); } catch { }
        }

        PopulatePeriodComboBox();
        RebuildScopedAndRefresh();
    }

    private MenuFlyout BuildXmlActionsFlyout(bool includeReprocess = true)
    {
        var flyout = new MenuFlyout();
        var miFileZip = new MenuItem { Header = "Selecionar Arquivo(s) XML ou Pacote ZIP..." };
        miFileZip.Click += async (_, _) => await PickAndOpenXmlAsync();
        var miFolder = new MenuItem { Header = "Selecionar Pasta de XMLs (lê subpastas)..." };
        miFolder.Click += async (_, _) => await PickAndOpenXmlFolderAsync();
        flyout.Items.Add(miFileZip);
        flyout.Items.Add(miFolder);

        if (includeReprocess)
        {
            flyout.Items.Add(new Separator());
            var miReproc = new MenuItem { Header = "Reprocessar Último Pacote / Pasta (Datas / Faltantes)..." };
            miReproc.Click += async (_, _) => await ReloadAndReprocessLastXmlAsync();
            flyout.Items.Add(miReproc);
        }
        return flyout;
    }

    private async Task PickAndOpenXmlAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar Pacote ZIP ou XML(s) Fiscal(is)",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Pacotes ZIP / XML") { Patterns = new[] { "*.zip", "*.xml", "*.ZIP", "*.XML" } },
                new FilePickerFileType("Todos os arquivos") { Patterns = new[] { "*.*" } }
            }
        });
        if (files.Count == 0) return;

        var paths = files.Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();
        if (paths.Count == 0) return;

        string label = paths.Count == 1 ? Path.GetFileName(paths[0]) : $"{paths.Count} arquivo(s) XML";
        string? dbPath = paths.Count == 1 ? paths[0] : Path.GetDirectoryName(paths[0]);
        await LoadXmlSourcesAsync(paths, label, dbPath);
    }

    private async Task PickAndOpenXmlFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Selecionar Pasta com XMLs / ZIPs (lê todas as subpastas)",
            AllowMultiple = false
        });
        if (folders.Count == 0) return;
        string? path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

        string dirName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(dirName)) dirName = path;
        await LoadXmlSourcesAsync(new[] { path }, $"Pasta: {dirName}", path);
    }

    private async Task ReloadAndReprocessLastXmlAsync()
    {
        if (!_hasDocument)
        {
            await ShowInfoDialogAsync("Nenhum Sintegra Aberto", "Abra primeiro o arquivo Sintegra (.txt) onde deseja reprocessar os XMLs.");
            return;
        }

        var existingSources = _loadedXmlSourcePaths
            .Where(p => File.Exists(p) || Directory.Exists(p))
            .ToList();

        if (existingSources.Count == 0)
        {
            string? savedXml = _db.GetSetting("last_xml_path");
            if (!string.IsNullOrWhiteSpace(savedXml) && (File.Exists(savedXml) || Directory.Exists(savedXml)))
                existingSources.Add(savedXml);
        }

        if (existingSources.Count == 0)
        {
            await ShowInfoDialogAsync(
                "Nenhum XML / Pasta Salvo",
                "Carregue primeiro uma Pasta de XMLs ou pacote ZIP (em 'Carregar XML / Pasta') para depois reprocessar sem precisar selecionar novamente.");
            return;
        }

        string first = existingSources[0];
        string label = existingSources.Count > 1
            ? $"{existingSources.Count} arquivo(s) XML"
            : (Directory.Exists(first) ? $"Pasta: {Path.GetFileName(first.TrimEnd('\\', '/'))}" : Path.GetFileName(first));

        bool loaded = await LoadXmlSourcesAsync(existingSources, label, existingSources.Count == 1 ? first : null);
        if (loaded)
            await OpenReprocessXmlDialogAsync();
    }

    private Task LoadXmlOrZipAsync(string path)
    {
        string label = Directory.Exists(path)
            ? $"Pasta: {Path.GetFileName(path.TrimEnd('\\', '/'))}"
            : Path.GetFileName(path);
        return LoadXmlSourcesAsync(new[] { path }, label, path);
    }

    private async Task<bool> LoadXmlSourcesAsync(IReadOnlyList<string> paths, string label, string? dbSettingPath)
    {
        var dlg = new ImportProgressWindow("Importando XML / ZIP / Pasta");
        var progress = new Progress<(int Percent, string Status)>(p => dlg.UpdateProgress(p.Percent, p.Status));
        List<XmlDocumentSummary>? xmls = null;
        AuditReport? result = null;
        Exception? failure = null;

        dlg.Opened += async (_, _) =>
        {
            try
            {
                await Task.Run(() =>
                {
                    xmls = FiscalCrossAuditor.LoadXmlsFromPaths(paths, progress, dlg.Token);
                    if (_hasDocument)
                    {
                        ((IProgress<(int, string)>)progress).Report((95, $"Cruzando {xmls.Count} XML(s) com o Sintegra..."));
                        result = FiscalCrossAuditor.RunCrossAudit(_doc, xmls, _loadedSped);
                    }
                    ((IProgress<(int, string)>)progress).Report((100, "Confronto concluído."));
                }, dlg.Token);
            }
            catch (OperationCanceledException) { failure = new OperationCanceledException(); }
            catch (Exception ex) { failure = ex; }
            finally { dlg.Close(); }
        };

        await dlg.ShowDialog(this);

        if (failure is OperationCanceledException) return false;
        if (failure is not null)
        {
            await ShowInfoDialogAsync("Erro na leitura de XML", failure.Message);
            return false;
        }
        if (xmls is null) return false;

        _loadedXmlDocs = xmls;
        _loadedXmlSourcePaths = paths.ToList();
        _audit = result;
        _lastXmlSourceLabel = label;
        if (!string.IsNullOrWhiteSpace(dbSettingPath))
        {
            try { _db.SetSetting("last_xml_path", dbSettingPath); } catch { }
        }

        if (_hasDocument && _doc.Reg10 is not null)
        {
            try { _db.RecordImport(_doc, _report, _audit, _currentFileName, _lastXmlSourceLabel); } catch { }
        }
        RebuildScopedAndRefresh();
        return true;
    }

    private async Task PickAndOpenSpedAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar SPED Fiscal EFD ICMS/IPI (.txt)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("SPED Fiscal (*.txt)") { Patterns = new[] { "*.txt", "*.TXT" } }
            }
        });
        if (files.Count == 0) return;
        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        var dlg = new ImportProgressWindow("Importando SPED Fiscal");
        SpedParsedData? sped = null;
        AuditReport? result = null;
        Exception? failure = null;

        dlg.Opened += async (_, _) =>
        {
            try
            {
                await Task.Run(() =>
                {
                    dlg.UpdateProgress(30, $"Processando SPED: {Path.GetFileName(path)}");
                    sped = FiscalCrossAuditor.ParseSpedFiscalFile(path);
                    if (_hasDocument)
                    {
                        dlg.UpdateProgress(80, $"Confrontando {sped.Invoices.Count} documentos C100...");
                        result = FiscalCrossAuditor.RunCrossAudit(_doc, _loadedXmlDocs, sped);
                    }
                    dlg.UpdateProgress(100, "Confronto concluído.");
                }, dlg.Token);
            }
            catch (OperationCanceledException) { failure = new OperationCanceledException(); }
            catch (Exception ex) { failure = ex; }
            finally { dlg.Close(); }
        };

        await dlg.ShowDialog(this);
        if (failure is OperationCanceledException || failure is not null) return;

        _loadedSped = sped;
        _audit = result;
        RebuildScopedAndRefresh();
        NavigateTo(NavSection.NotasFaltantes);
    }

    private async Task OpenInjectMissingDialogAsync(XmlAuditDocumentRow? singleGap)
    {
        if (!_hasDocument)
        {
            await ShowInfoDialogAsync("Nenhum Sintegra Aberto", "Abra primeiro o arquivo Sintegra (.txt) onde deseja lançar as notas faltantes.");
            return;
        }

        if (_audit is null || _loadedXmlDocs.Count == 0)
        {
            await PickAndOpenXmlAsync();
            if (_audit is null || _loadedXmlDocs.Count == 0) return;
        }

        var gapsToFind = singleGap is not null
            ? new List<XmlAuditDocumentRow> { singleGap }
            : _scoped.XmlDocumentRows.Where(r => r.IsCriticalGap).ToList();

        if (gapsToFind.Count == 0)
        {
            await ShowInfoDialogAsync("Sem Notas Faltantes", "Todas as notas fiscais de compra do período já constam lançadas neste Sintegra.");
            return;
        }

        var items = new List<XmlPurchaseProcessCandidate>();
        foreach (var g in gapsToFind)
        {
            var found = FindLoadedXmlSummary(g);
            if (found is not null)
                items.Add(new XmlPurchaseProcessCandidate(found, AlreadyInSintegra: false));
        }

        if (items.Count == 0)
        {
            await ShowInfoDialogAsync("XMLs Indisponíveis", "Recarregue o pacote ZIP ou a pasta de XMLs para realizar o lançamento detalhado no Sintegra.");
            return;
        }

        await RunXmlProcessDialogAsync(items, isReprocessMode: false);
    }

    private async Task OpenReprocessXmlDialogAsync()
    {
        if (!_hasDocument)
        {
            await ShowInfoDialogAsync("Nenhum Sintegra Aberto", "Abra primeiro o arquivo Sintegra (.txt) para reprocessar os XMLs.");
            return;
        }

        if (_audit is null || _loadedXmlDocs.Count == 0)
        {
            await PickAndOpenXmlAsync();
            if (_audit is null || _loadedXmlDocs.Count == 0) return;
        }

        var items = FiscalCrossAuditor.BuildPurchaseProcessCandidates(_doc, _loadedXmlDocs, _selectedYearMonth);
        if (items.Count == 0)
        {
            await ShowInfoDialogAsync("Sem XMLs de Compra", "Nenhuma NF-e de compra encontrada nos XMLs para a competência selecionada.");
            return;
        }

        await RunXmlProcessDialogAsync(items, isReprocessMode: true);
    }

    private async Task RunXmlProcessDialogAsync(IReadOnlyList<XmlPurchaseProcessCandidate> items, bool isReprocessMode)
    {
        string buyerUf = _doc.Reg10?.Uf.Trim() ?? "MG";
        string regime = _doc.Reg10 is not null ? _db.GetCompanyTaxRegime(_doc.Reg10.Cnpj) : "Simples Nacional";

        var dlg = new InjectMissingXmlWindow(
            items,
            buyerUf,
            regime,
            isReprocessMode,
            hasXmlSource: _loadedXmlSourcePaths.Count > 0);
        bool? confirmed = await dlg.ShowDialog<bool?>(this);
        if (confirmed != true || dlg.ConfirmedRequests is null || dlg.ConfirmedRequests.Count == 0)
            return;

        var res = isReprocessMode
            ? SintegraXmlInjector.ReprocessXmlInvoices(_currentBytes, dlg.ConfirmedRequests, dlg.SimplesZeroIcms, dlg.IncludeItems54)
            : SintegraXmlInjector.InjectXmlInvoices(_currentBytes, dlg.ConfirmedRequests, dlg.SimplesZeroIcms, dlg.IncludeItems54);

        _currentBytes = res.UpdatedBytes;
        _hasUnsavedChanges = true;
        _lastFixReport = res.FixReport;
        _doc = SintegraParser.ParseBytes(_currentBytes);
        _report = res.AfterReport;
        _audit = FiscalCrossAuditor.RunCrossAudit(_doc, _loadedXmlDocs, _loadedSped);

        string? autoSavedPath = null;
        if (dlg.AutoSaveUpdatedSintegra && !string.IsNullOrEmpty(_currentFilePath))
        {
            autoSavedPath = BuildUpdatedSintegraPath(_currentFilePath);
            await File.WriteAllBytesAsync(autoSavedPath, _currentBytes);
            _hasUnsavedChanges = false;
            _currentFilePath = autoSavedPath;
            _currentFileName = Path.GetFileName(autoSavedPath);
            try { _db.SetSetting("last_sintegra_path", autoSavedPath); } catch { }
        }

        XmlCopyExportResult? xmlCopyResult = null;
        if (dlg.ExportAdjustedXmlCopies && _loadedXmlSourcePaths.Count > 0)
        {
            string outXmlDir = ResolveAdjustedXmlOutputDirectory();
            xmlCopyResult = await Task.Run(() =>
                SintegraXmlInjector.ExportAdjustedXmlCopies(_loadedXmlSourcePaths, outXmlDir, dlg.ConfirmedRequests));
        }

        if (_doc.Reg10 is not null)
        {
            try { _db.RecordImport(_doc, _report, _audit, _currentFileName, _lastXmlSourceLabel); } catch { }
        }

        PopulatePeriodComboBox();
        RebuildScopedAndRefresh();

        var msg = new StringBuilder();
        if (res.InjectedInvoicesCount > 0)
            msg.AppendLine($"• {res.InjectedInvoicesCount} nota(s) incluída(s) ({res.GeneratedReg54Items} itens Reg. 54, {res.GeneratedReg75Products} produtos Reg. 75).");
        if (res.UpdatedExistingCount > 0)
            msg.AppendLine($"• {res.UpdatedExistingCount} nota(s) já lançada(s) atualizada(s) no Registro 50 (Data de Entrada / CFOP).");
        if (res.InjectedInvoicesCount == 0 && res.UpdatedExistingCount == 0)
            msg.AppendLine("• Nenhuma alteração de data ou CFOP necessária no Sintegra (valores mantidos).");

        msg.AppendLine();
        msg.AppendLine($"Validação após processamento: {res.AfterReport.Summary.ErrorCount} erro(s), {res.AfterReport.Summary.WarningCount} aviso(s).");

        if (autoSavedPath is not null)
        {
            msg.AppendLine();
            msg.AppendLine($"Sintegra salvo automaticamente em:\n{autoSavedPath}");
        }
        else
        {
            msg.AppendLine();
            msg.AppendLine("Clique em 'Salvar Sintegra Atualizado' no topo para gravar o arquivo em disco.");
        }

        if (xmlCopyResult is not null)
        {
            msg.AppendLine();
            msg.AppendLine($"Cópias de XML ajustadas ({xmlCopyResult.ExportedCount} arquivo(s)) salvas em:\n{xmlCopyResult.OutputDirectory}");
            msg.AppendLine("(Os XMLs originais permaneceram intactos para preservar a assinatura SEFAZ.)");
        }

        await ShowInfoDialogAsync(
            isReprocessMode ? "Reprocessamento Concluído!" : "Notas Lançadas no Sintegra!",
            msg.ToString().TrimEnd());
    }

    private static string BuildUpdatedSintegraPath(string currentPath)
    {
        string dir = Path.GetDirectoryName(currentPath) ?? ".";
        string nameNoExt = Path.GetFileNameWithoutExtension(currentPath);
        string ext = Path.GetExtension(currentPath);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".txt";

        if (nameNoExt.EndsWith("_ATUALIZADO", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(dir, $"{nameNoExt}{ext}");

        return Path.Combine(dir, $"{nameNoExt}_ATUALIZADO{ext}");
    }

    private string ResolveAdjustedXmlOutputDirectory()
    {
        string first = _loadedXmlSourcePaths[0];
        if (Directory.Exists(first))
            return Path.Combine(first, "XMLs_Ajustados");

        if (!string.IsNullOrEmpty(_currentFilePath))
            return Path.Combine(Path.GetDirectoryName(_currentFilePath) ?? ".", "XMLs_Ajustados");

        string parent = Path.GetDirectoryName(first) ?? ".";
        return Path.Combine(parent, Path.GetFileNameWithoutExtension(first) + "_XMLs_Ajustados");
    }

    private async Task RunQuickFixAndRefreshAsync()
    {
        if (!_hasDocument)
        {
            await ShowInfoDialogAsync("Nenhum Arquivo Aberto", "Abra um arquivo Sintegra (.txt) primeiro.");
            return;
        }

        var fx = SintegraAutoFixer.AutoFix(_currentBytes, new FixOptions());
        _lastFixReport = fx;
        _currentBytes = fx.FixedBytes;
        _hasUnsavedChanges = true;
        _doc = SintegraParser.ParseBytes(_currentBytes);
        _report = SintegraValidator.Validate(_doc);
        if (_loadedXmlDocs.Count > 0 || _loadedSped is not null)
            _audit = FiscalCrossAuditor.RunCrossAudit(_doc, _loadedXmlDocs, _loadedSped);

        PopulatePeriodComboBox();
        RebuildScopedAndRefresh();

        await ShowInfoDialogAsync(
            "Correção Automática Concluída",
            $"Erros: {fx.BeforeSummary.ErrorCount} -> {fx.AfterSummary.ErrorCount}\n" +
            $"Avisos: {fx.BeforeSummary.WarningCount} -> {fx.AfterSummary.WarningCount}\n\n" +
            "Clique em 'Salvar Sintegra Atualizado' para gravar o arquivo corrigido.");
    }

    private async Task SaveCurrentBytesDialogAsync()
    {
        if (!_hasDocument || _currentBytes.Length == 0) return;

        string suggested = Path.GetFileNameWithoutExtension(_currentFileName) + "_ATUALIZADO.txt";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Arquivo Sintegra (.txt)",
            SuggestedFileName = suggested,
            DefaultExtension = "txt"
        });
        if (file is null) return;
        string? outPath = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(outPath)) return;

        await File.WriteAllBytesAsync(outPath, _currentBytes);
        _hasUnsavedChanges = false;
        _currentFilePath = outPath;
        _currentFileName = Path.GetFileName(outPath);
        try { _db.SetSetting("last_sintegra_path", outPath); } catch { }
        UpdateHeaderAndStatusBar();

        await ShowInfoDialogAsync("Arquivo Sintegra Salvo", $"Arquivo salvo com sucesso em:\n{outPath}");
    }

    private async Task ExportMonthlySplitsDialogAsync()
    {
        if (!_hasDocument) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Selecionar Pasta para Arquivos Mensais",
            AllowMultiple = false
        });
        if (folders.Count == 0) return;
        string? dir = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(dir)) return;

        var splits = SintegraAutoFixer.SplitAnnualByMonth(_currentBytes);
        foreach (var sp in splits)
            await File.WriteAllBytesAsync(Path.Combine(dir, sp.SuggestedFileName), sp.FileBytes);

        await ShowInfoDialogAsync(
            "Arquivos Mensais Gerados",
            $"{splits.Count} arquivo(s) mensais individuais validados salvos em:\n{dir}");
    }

    private FiscalReportContext BuildCurrentReportContext(ReportMode mode = ReportMode.Completo)
    {
        string regime = _doc.Reg10 is not null ? _db.GetCompanyTaxRegime(_doc.Reg10.Cnpj) : "Simples Nacional";
        return new FiscalReportContext(_doc, _report, _scoped, _audit, regime, AppVersion, mode);
    }

    private void OpenReportPreview(ReportMode mode = ReportMode.Completo)
    {
        if (!_hasDocument) return;
        var win = new ReportPreviewWindow(BuildCurrentReportContext(mode));
        win.ShowDialog(this);
    }

    private async Task ExportDirectPdfAsync()
    {
        if (!_hasDocument) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Relatório em PDF",
            SuggestedFileName = $"Relatorio_Sintegra_{_scoped.SelectedYearMonth ?? "Anual"}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { new FilePickerFileType("Documento PDF") { Patterns = new[] { "*.pdf" } } }
        });
        if (file is null) return;
        string? path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        PdfReportGenerator.GeneratePdf(BuildCurrentReportContext(ReportMode.Completo), path);
        await ShowInfoDialogAsync("PDF Gerado", $"Relatório salvo em:\n{path}");
    }

    private async Task ExportDirectXlsxAsync()
    {
        if (!_hasDocument) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Planilha Excel (.xlsx)",
            SuggestedFileName = $"Planilha_Sintegra_{_scoped.SelectedYearMonth ?? "Anual"}.xlsx",
            DefaultExtension = "xlsx",
            FileTypeChoices = new[] { new FilePickerFileType("Planilha Excel") { Patterns = new[] { "*.xlsx" } } }
        });
        if (file is null) return;
        string? path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        ExcelReportGenerator.GenerateWorkbook(BuildCurrentReportContext(ReportMode.Completo), path);
        await ShowInfoDialogAsync("Excel Gerado", $"Planilha salva em:\n{path}");
    }

    private void OpenInvoiceDetails(Reg50 invoice)
    {
        long normNum = long.TryParse(new string(invoice.Numero.Where(char.IsAsciiDigit).ToArray()), out long n) ? n % 1_000_000L : 0;
        var xmlMatch = _audit?.DocumentRows.FirstOrDefault(x =>
            x.EmitCnpj.Trim() == invoice.Cnpj.Trim() &&
            long.TryParse(new string(x.Numero.Where(char.IsAsciiDigit).ToArray()), out long xn) && (xn % 1_000_000L) == normNum);

        var win = new InvoiceDetailsWindow(invoice, _doc, xmlMatch);
        win.ShowDialog(this);
    }

    private XmlDocumentSummary? FindLoadedXmlSummary(XmlAuditDocumentRow row) =>
        _loadedXmlDocs.FirstOrDefault(x =>
            x.EmitCnpj.Trim() == row.EmitCnpj.Trim() &&
            x.Numero.Trim().TrimStart('0') == row.Numero.Trim().TrimStart('0') &&
            x.Serie.Trim().TrimStart('0') == row.Serie.Trim().TrimStart('0') &&
            x.Modelo.Trim() == row.Modelo.Trim())
        ?? _loadedXmlDocs.FirstOrDefault(x =>
            x.EmitCnpj.Trim() == row.EmitCnpj.Trim() &&
            x.Numero.Trim().TrimStart('0') == row.Numero.Trim().TrimStart('0') &&
            x.Modelo.Trim() == row.Modelo.Trim());

    private async Task OpenXmlInvoiceDetailsAsync(XmlAuditDocumentRow row)
    {
        var xml = FindLoadedXmlSummary(row);
        if (xml is null)
        {
            await ShowInfoDialogAsync(
                "Detalhes da Nota XML",
                $"Nota: {row.Numero}/{row.Serie} (Modelo {row.Modelo})\n" +
                $"Fornecedor: {row.EmitNome} ({TaxIdValidator.FormatCnpj(row.EmitCnpj)})\n" +
                $"Emissão: {row.DataBr}  |  Valor XML: R$ {TaxIdValidator.FormatCentsBrl(row.XmlValorCents)}\n" +
                $"Status: {row.MatchStatus}\n\n" +
                "Recarregue o pacote ZIP/pasta de XMLs para inspecionar a lista completa de produtos.");
            return;
        }

        var dlg = new XmlInvoiceDetailsWindow(xml, row, _hasDocument && row.IsCriticalGap);
        await dlg.ShowDialog(this);
        if (dlg.LaunchRequested)
            await OpenInjectMissingDialogAsync(row);
    }

    private void OpenCouponMonthDetails(Reg61 couponDay)
    {
        var win = new CouponMonthDetailsWindow(couponDay, _doc);
        win.ShowDialog(this);
    }

    private void InspectLineAt(int lineNumber, int startCol = 1, int endCol = 2)
    {
        if (lineNumber < 1 || lineNumber > _doc.Lines.Count) return;
        var win = new LineInspectorWindow(_doc.Lines[lineNumber - 1], startCol, endCol);
        win.ShowDialog(this);
    }

    private void SelectPeriodInCombo(string yearMonth)
    {
        for (int i = 0; i < _cmbPeriod.Items.Count; i++)
        {
            if (_cmbPeriod.Items[i] is ComboBoxItem item && Equals(item.Tag, yearMonth))
            {
                _cmbPeriod.SelectedIndex = i;
                return;
            }
        }
    }

    private Control BuildMonthlyFilterHintBadge()
    {
        if (!_scoped.IsSingleMonthFilter)
        {
            return new TextBlock
            {
                Text = "Clique em um mês para filtrar",
                FontSize = 11,
                Foreground = BrushMuted,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        var btnReset = new Button
        {
            Content = "Exibir todos os meses",
            FontSize = 11,
            Padding = new Thickness(8, 3),
            Background = Brush.Parse("#EFF6FF"),
            BorderBrush = Brush.Parse("#BFDBFE"),
            BorderThickness = new Thickness(1),
            Foreground = BrushBlueDark,
            CornerRadius = new CornerRadius(4)
        };
        btnReset.Click += (_, _) => SelectPeriodInCombo("ALL");
        return btnReset;
    }

    private static Control BuildBarLegend()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(CreateLegendDot("#2563EB", "Entradas"));
        sp.Children.Add(CreateLegendDot("#15803D", "Saídas"));
        return sp;
    }

    private static Control CreateLegendDot(string hex, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        row.Children.Add(new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(2),
            Background = Brush.Parse(hex),
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(new TextBlock { Text = text, FontSize = 11.5, Foreground = Brush.Parse("#475569") });
        return row;
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        var dlg = new Window
        {
            Title = title,
            Icon = VectorIcons.AppWindowIcon,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White
        };
        var sp = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        sp.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Brush.Parse("#1E293B") });
        var btnOk = CreatePrimaryButton("OK", VectorIcons.CheckBadge, () => dlg.Close());
        btnOk.HorizontalAlignment = HorizontalAlignment.Right;
        sp.Children.Add(btnOk);
        dlg.Content = sp;
        await dlg.ShowDialog(this);
    }

    // ==================== SHARED UI HELPERS & VIRTUALIZED TABLE ====================

    private static Control BuildEmptyTableWithHeader(
        GridLength[] cols,
        Action<Grid> buildHeader,
        string emptyMessage) => new Border
    {
        BorderBrush = BrushBorderLight,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Child = new StackPanel
        {
            Children =
            {
                UiTableHelpers.WrapWithHeader(cols, buildHeader, new ListBox { Height = 0, IsVisible = false }),
                new TextBlock { Text = emptyMessage, Margin = new Thickness(12), Foreground = BrushMuted, FontSize = 12 }
            }
        }
    };

    private static Border BuildKpiCard(
        string iconPath,
        string iconHex,
        string iconBgHex,
        string label,
        string bigValue,
        string subText,
        (string Text, string BgHex, string FgHex)? badge,
        string? bigValueHex = null)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#D5DEE8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12)
        };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var icoCircle = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(21),
            Background = Brush.Parse(iconBgHex),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Child = VectorIcons.Create(iconPath, iconHex, 20)
        };
        Grid.SetColumn(icoCircle, 0);
        row.Children.Add(icoCircle);

        var col = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        col.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Brush.Parse("#475569") });

        var valRow = new WrapPanel { Orientation = Orientation.Horizontal };
        valRow.Children.Add(new TextBlock
        {
            Text = bigValue,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = bigValueHex is not null ? Brush.Parse(bigValueHex) : BrushTextDark,
            Margin = new Thickness(0, 0, 8, 2),
            VerticalAlignment = VerticalAlignment.Center
        });
        if (badge is { } b)
        {
            valRow.Children.Add(new Border
            {
                Background = Brush.Parse(b.BgHex),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2),
                Margin = new Thickness(0, 2, 0, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = b.Text, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse(b.FgHex) }
            });
        }
        col.Children.Add(valRow);
        col.Children.Add(new TextBlock
        {
            Text = subText,
            FontSize = 11.5,
            Foreground = BrushMuted,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        Grid.SetColumn(col, 1);
        row.Children.Add(col);
        card.Child = row;
        return card;
    }

    private static Border CreateCardShell(string title, string iconPath, Control? rightHeaderControl)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#D5DEE8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 11)
        };

        var stack = new StackPanel { Spacing = 8 };
        var hdr = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        var ico = VectorIcons.Create(iconPath, "#475569", 16);
        ico.Margin = new Thickness(0, 0, 7, 0);
        Grid.SetColumn(ico, 0);
        hdr.Children.Add(ico);

        var lbl = new TextBlock
        {
            Text = title,
            FontSize = 13.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = BrushTextDark,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(lbl, 1);
        hdr.Children.Add(lbl);

        if (rightHeaderControl is not null)
        {
            Grid.SetColumn(rightHeaderControl, 2);
            hdr.Children.Add(rightHeaderControl);
        }

        stack.Children.Add(hdr);
        card.Child = stack;
        return card;
    }

    private static Control CreateDonutLegendItem(string colorHex, string label, string valText, string pctText)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(2),
            Background = Brush.Parse(colorHex),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0)
        });
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = BrushTextDark });
        sp.Children.Add(new TextBlock { Text = valText, FontSize = 11.5, Foreground = BrushTextBody });
        if (!string.IsNullOrEmpty(pctText))
            sp.Children.Add(new TextBlock { Text = pctText, FontSize = 11, Foreground = BrushMuted });
        row.Children.Add(sp);
        return row;
    }

    private static Control CreateAlertItem(bool isError, string title, string subtitle, Action onClick)
    {
        var btn = new Button();
        btn.Classes.Add("alert");
        if (isError) btn.Classes.Add("danger");

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var ico = VectorIcons.Create(isError ? VectorIcons.Warning : VectorIcons.CheckBadge, isError ? "#B91C1C" : "#15803D", 17);
        ico.Margin = new Thickness(0, 2, 9, 0);
        Grid.SetColumn(ico, 0);
        grid.Children.Add(ico);

        var sp = new StackPanel { Spacing = 1 };
        sp.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = BrushTextDark });
        sp.Children.Add(new TextBlock { Text = subtitle, FontSize = 11.5, Foreground = Brush.Parse("#475569"), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(sp, 1);
        grid.Children.Add(sp);

        btn.Content = grid;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button CreateQuickActionButton(string label, string iconPath, Action? onClick)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(VectorIcons.Create(iconPath, "#1D4ED8", 15));
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            Foreground = Brush.Parse("#1E293B"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var btn = new Button
        {
            Content = row
        };
        btn.Classes.Add("quick");
        if (onClick is not null)
            btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button CreatePrimaryButton(string label, string iconPath, Action onClick)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        row.Children.Add(VectorIcons.Create(iconPath, "#FFFFFF", 15));
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });

        var btn = new Button
        {
            Content = row
        };
        btn.Classes.Add("primary");
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button CreateFilterChip(string text, bool active, Action onClick)
    {
        var btn = new Button
        {
            Content = text
        };
        btn.Classes.Add("chip");
        if (active) btn.Classes.Add("active");
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Border BuildSmallStatBox(string title, string value, string valueHex) => new()
    {
        Background = Brushes.White,
        BorderBrush = Brush.Parse("#D5DEE8"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(12, 10),
        Child = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = title, FontSize = 11.5, Foreground = BrushMuted },
                new TextBlock { Text = value, FontSize = 17, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(valueHex) }
            }
        }
    };

    private static void AddHeaderCell(Grid g, int col, string text, TextAlignment align)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 11.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = BrushTextBody,
            TextAlignment = align,
            Padding = CellPad,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(tb, col);
        g.Children.Add(tb);
    }

    private static void AddBodyCellFast(Grid g, int col, string text, TextAlignment align, FontWeight weight, IBrush fg)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 11.5,
            FontWeight = weight,
            Foreground = fg,
            TextAlignment = align,
            Padding = CellPad,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(tb, col);
        g.Children.Add(tb);
    }
}
