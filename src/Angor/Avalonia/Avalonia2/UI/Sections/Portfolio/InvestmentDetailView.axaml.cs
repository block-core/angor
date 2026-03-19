using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia2.UI.Shell;
using Avalonia2.UI.Shared;
using ReactiveUI;

namespace Avalonia2.UI.Sections.Portfolio;

public partial class InvestmentDetailView : UserControl
{
    private IDisposable? _layoutSubscription;

    // Cached FindControl results — avoid repeated tree walks (matches ProjectDetailView pattern)
    private DockPanel? _navBar;
    private StackPanel? _progressSteps;
    private StackPanel? _mobileProgressStages;
    private Panel? _navSpacer;
    private Grid? _topSectionGrid;
    private Border? _topLeftCard;
    private Panel? _topRightCard;
    private Grid? _statsGrid;
    private Border? _stat4Card0;
    private Border? _stat4Card1;
    private Border? _stat4Card2;
    private Border? _stat4Card3;
    private Grid? _infoGrid;
    private Border? _info4Card0;
    private Border? _info4Card1;
    private Border? _info4Card2;
    private Border? _info4Card3;
    private StackPanel? _stagesTableDesktop;
    private ItemsControl? _stagesCardsMobile;
    private StackPanel? _contentStack;

    public InvestmentDetailView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Cache all controls once
        _navBar = this.FindControl<DockPanel>("NavBar");
        _progressSteps = this.FindControl<StackPanel>("ProgressSteps");
        _mobileProgressStages = this.FindControl<StackPanel>("MobileProgressStages");
        _navSpacer = this.FindControl<Panel>("NavSpacer");
        _topSectionGrid = this.FindControl<Grid>("TopSectionGrid");
        _topLeftCard = this.FindControl<Border>("TopLeftCard");
        _topRightCard = this.FindControl<Panel>("TopRightCard");
        _statsGrid = this.FindControl<Grid>("StatsGrid4");
        _stat4Card0 = this.FindControl<Border>("Stat4Card0");
        _stat4Card1 = this.FindControl<Border>("Stat4Card1");
        _stat4Card2 = this.FindControl<Border>("Stat4Card2");
        _stat4Card3 = this.FindControl<Border>("Stat4Card3");
        _infoGrid = this.FindControl<Grid>("InfoGrid4");
        _info4Card0 = this.FindControl<Border>("Info4Card0");
        _info4Card1 = this.FindControl<Border>("Info4Card1");
        _info4Card2 = this.FindControl<Border>("Info4Card2");
        _info4Card3 = this.FindControl<Border>("Info4Card3");
        _stagesTableDesktop = this.FindControl<StackPanel>("StagesTableDesktop");
        _stagesCardsMobile = this.FindControl<ItemsControl>("StagesCardsMobile");
        _contentStack = this.FindControl<StackPanel>("ContentStack");

        // Subscribe to layout mode changes for responsive behavior
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    /// <summary>
    /// Applies responsive layout changes based on compact vs desktop mode.
    /// Vue breakpoints: <=1024px → top section stacks, stats 2x2; <=768px → nav hidden, mobile stages shown, stats 1-col, table→cards
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_topSectionGrid == null) return; // controls not yet loaded

        if (isCompact)
        {
            // --- Nav bar: hide on compact, show mobile progress stages ---
            // Vue: @media (max-width: 768px) { .sticky-nav-bar { display: none; } .mobile-progress-stages { display: flex; } }
            if (_navBar != null) _navBar.IsVisible = false;
            if (_mobileProgressStages != null) _mobileProgressStages.IsVisible = true;
            if (_navSpacer != null) _navSpacer.Height = 0; // No nav spacer needed

            // --- Top section: single column stacked ---
            // Vue: @media (max-width: 1024px) { .top-section { grid-template-columns: 1fr; } }
            _topSectionGrid.ColumnDefinitions.Clear();
            _topSectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _topSectionGrid.RowDefinitions.Clear();
            _topSectionGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _topSectionGrid.RowDefinitions.Add(new RowDefinition(new GridLength(16)));
            _topSectionGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            if (_topLeftCard != null)
            {
                Grid.SetColumn(_topLeftCard, 0);
                Grid.SetRow(_topLeftCard, 0);
                _topLeftCard.Margin = new Thickness(0, 0, 0, 0);
            }
            if (_topRightCard != null)
            {
                Grid.SetColumn(_topRightCard, 0);
                Grid.SetRow(_topRightCard, 2);
            }

            // --- Stats grid: single column stacked ---
            // Vue: @media (max-width: 768px) { .stats-grid { grid-template-columns: 1fr; gap: 12px; } }
            if (_statsGrid != null)
            {
                _statsGrid.ColumnDefinitions.Clear();
                _statsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _statsGrid.RowDefinitions.Clear();
                for (int i = 0; i < 4; i++)
                    _statsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
            SetStatCardCompact(_stat4Card0, 0);
            SetStatCardCompact(_stat4Card1, 1);
            SetStatCardCompact(_stat4Card2, 2);
            SetStatCardCompact(_stat4Card3, 3);

            // --- Info grid: single column stacked ---
            if (_infoGrid != null)
            {
                _infoGrid.ColumnDefinitions.Clear();
                _infoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _infoGrid.RowDefinitions.Clear();
                for (int i = 0; i < 4; i++)
                    _infoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
            SetInfoCardCompact(_info4Card0, 0);
            SetInfoCardCompact(_info4Card1, 1);
            SetInfoCardCompact(_info4Card2, 2);
            SetInfoCardCompact(_info4Card3, 3);

            // --- Stages: show cards, hide table ---
            // Vue: @media (max-width: 768px) { .stages-table-container { display: none; } .stages-cards-container { display: flex; } }
            if (_stagesTableDesktop != null) _stagesTableDesktop.IsVisible = false;
            if (_stagesCardsMobile != null) _stagesCardsMobile.IsVisible = true;

            // --- Bottom padding: 96px clearance for tab bar + floating panel ---
            if (_contentStack != null) _contentStack.Margin = new Thickness(0, 0, 0, 96);
        }
        else
        {
            // --- Desktop: restore all layouts ---

            // Nav bar visible
            if (_navBar != null) _navBar.IsVisible = true;
            if (_mobileProgressStages != null) _mobileProgressStages.IsVisible = false;
            if (_navSpacer != null) _navSpacer.Height = 92;

            // Top section: 2 columns (1fr, 400px)
            _topSectionGrid.ColumnDefinitions.Clear();
            _topSectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _topSectionGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(400)));
            _topSectionGrid.RowDefinitions.Clear();

            if (_topLeftCard != null)
            {
                Grid.SetColumn(_topLeftCard, 0);
                Grid.SetRow(_topLeftCard, 0);
                _topLeftCard.Margin = new Thickness(0, 0, 24, 0);
            }
            if (_topRightCard != null)
            {
                Grid.SetColumn(_topRightCard, 1);
                Grid.SetRow(_topRightCard, 0);
            }

            // Stats grid: 4 columns
            if (_statsGrid != null)
            {
                _statsGrid.RowDefinitions.Clear();
                _statsGrid.ColumnDefinitions.Clear();
                for (int i = 0; i < 4; i++)
                    _statsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }
            SetStatCardDesktop(_stat4Card0, 0, new Thickness(0, 0, 8, 0));
            SetStatCardDesktop(_stat4Card1, 1, new Thickness(8, 0, 8, 0));
            SetStatCardDesktop(_stat4Card2, 2, new Thickness(8, 0, 8, 0));
            SetStatCardDesktop(_stat4Card3, 3, new Thickness(8, 0, 0, 0));

            // Info grid: 4 columns
            if (_infoGrid != null)
            {
                _infoGrid.RowDefinitions.Clear();
                _infoGrid.ColumnDefinitions.Clear();
                for (int i = 0; i < 4; i++)
                    _infoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }
            SetInfoCardDesktop(_info4Card0, 0, new Thickness(0, 0, 8, 0));
            SetInfoCardDesktop(_info4Card1, 1, new Thickness(8, 0, 8, 0));
            SetInfoCardDesktop(_info4Card2, 2, new Thickness(8, 0, 8, 0));
            SetInfoCardDesktop(_info4Card3, 3, new Thickness(8, 0, 0, 0));

            // Stages: show table, hide cards
            if (_stagesTableDesktop != null) _stagesTableDesktop.IsVisible = true;
            if (_stagesCardsMobile != null) _stagesCardsMobile.IsVisible = false;

            // Restore bottom padding
            if (_contentStack != null) _contentStack.Margin = new Thickness(0);
        }
    }

    private static void SetStatCardCompact(Border? card, int row)
    {
        if (card == null) return;
        Grid.SetColumn(card, 0);
        Grid.SetRow(card, row);
        card.Margin = new Thickness(0, row > 0 ? 12 : 0, 0, 0);
    }

    private static void SetStatCardDesktop(Border? card, int col, Thickness margin)
    {
        if (card == null) return;
        Grid.SetColumn(card, col);
        Grid.SetRow(card, 0);
        card.Margin = margin;
    }

    private static void SetInfoCardCompact(Border? card, int row)
    {
        if (card == null) return;
        Grid.SetColumn(card, 0);
        Grid.SetRow(card, row);
        card.Margin = new Thickness(0, row > 0 ? 12 : 0, 0, 0);
    }

    private static void SetInfoCardDesktop(Border? card, int col, Thickness margin)
    {
        if (card == null) return;
        Grid.SetColumn(card, col);
        Grid.SetRow(card, 0);
        card.Margin = margin;
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "BackButton":
                // Navigate back: find parent PortfolioView and call CloseInvestmentDetail
                var portfolioView = this.FindLogicalAncestorOfType<PortfolioView>();
                if (portfolioView?.DataContext is PortfolioViewModel vm)
                {
                    vm.CloseInvestmentDetail();
                }
                break;

            case "RecoverFundsButton":
                LaunchRecoveryModals();
                break;
        }
    }

    /// <summary>
    /// Opens the recovery modals overlay based on the current PenaltyState.
    /// State machine: none→RecoveryModal, pending→ClaimModal, canRelease→ReleaseModal.
    /// </summary>
    private void LaunchRecoveryModals()
    {
        if (DataContext is not InvestmentViewModel investVm) return;

        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        if (shellVm == null) return;

        // Set the appropriate modal visibility based on penalty state
        switch (investVm.PenaltyState)
        {
            case "none":
                investVm.ShowRecoveryModal = true;
                break;
            case "pending":
                investVm.ShowClaimModal = true;
                break;
            case "canRelease":
                investVm.ShowReleaseModal = true;
                break;
            default:
                return; // "released" — button shouldn't be visible
        }

        // Create the recovery modals view and wire up DataContext
        var recoveryModals = new RecoveryModalsView
        {
            DataContext = investVm
        };

        shellVm.ShowModal(recoveryModals);
    }
}
