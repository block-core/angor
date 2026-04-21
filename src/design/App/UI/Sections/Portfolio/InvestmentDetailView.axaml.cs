using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Angor.Shared.Services;
using App.UI.Shared;
using App.UI.Shell;
using App.UI.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace App.UI.Sections.Portfolio;

public partial class InvestmentDetailView : UserControl
{
    private IDisposable? _layoutSubscription;

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

        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    /// <summary>
    /// Applies responsive layout changes via in-place Grid mutations (SIGABRT-safe per project rule).
    /// Grids are declared with max col/row counts in XAML; this only mutates Widths/Heights and Grid.Row/Column attached properties.
    /// Vue breakpoints: &lt;=1024px → top section stacks, stats 2x2; &lt;=768px → nav hidden, mobile stages shown, stats 1-col, table→cards.
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_topSectionGrid == null) return; // XAML not yet loaded or names missing — no-op

        if (isCompact)
        {
            if (_navBar != null) _navBar.IsVisible = false;
            if (_mobileProgressStages != null) _mobileProgressStages.IsVisible = true;
            if (_navSpacer != null) _navSpacer.Height = 0;

            // Top section: collapse right column, stack in rows
            if (_topSectionGrid.ColumnDefinitions.Count >= 2)
                _topSectionGrid.ColumnDefinitions[1].Width = new GridLength(0);
            if (_topLeftCard != null)
            {
                Grid.SetColumn(_topLeftCard, 0);
                Grid.SetRow(_topLeftCard, 0);
                _topLeftCard.Margin = new Thickness(0);
            }
            if (_topRightCard != null)
            {
                Grid.SetColumn(_topRightCard, 0);
                Grid.SetRow(_topRightCard, 2);
            }

            // Stats grid: collapse cols 1-3, stack cards vertically
            if (_statsGrid != null)
            {
                for (int i = 1; i < _statsGrid.ColumnDefinitions.Count; i++)
                    _statsGrid.ColumnDefinitions[i].Width = new GridLength(0);
            }
            SetStatCardCompact(_stat4Card0, 0);
            SetStatCardCompact(_stat4Card1, 1);
            SetStatCardCompact(_stat4Card2, 2);
            SetStatCardCompact(_stat4Card3, 3);

            // Info grid: same pattern
            if (_infoGrid != null)
            {
                for (int i = 1; i < _infoGrid.ColumnDefinitions.Count; i++)
                    _infoGrid.ColumnDefinitions[i].Width = new GridLength(0);
            }
            SetInfoCardCompact(_info4Card0, 0);
            SetInfoCardCompact(_info4Card1, 1);
            SetInfoCardCompact(_info4Card2, 2);
            SetInfoCardCompact(_info4Card3, 3);

            if (_stagesTableDesktop != null) _stagesTableDesktop.IsVisible = false;
            if (_stagesCardsMobile != null) _stagesCardsMobile.IsVisible = true;

            // Bottom padding for tab bar clearance
            if (_contentStack != null) _contentStack.Margin = new Thickness(0, 0, 0, 96);
        }
        else
        {
            if (_navBar != null) _navBar.IsVisible = true;
            if (_mobileProgressStages != null) _mobileProgressStages.IsVisible = false;
            if (_navSpacer != null) _navSpacer.Height = 92;

            // Top section: restore right column to 400px fixed
            if (_topSectionGrid.ColumnDefinitions.Count >= 2)
                _topSectionGrid.ColumnDefinitions[1].Width = new GridLength(400);
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

            // Stats grid: restore cols to Star
            if (_statsGrid != null)
            {
                for (int i = 1; i < _statsGrid.ColumnDefinitions.Count; i++)
                    _statsGrid.ColumnDefinitions[i].Width = GridLength.Star;
            }
            SetStatCardDesktop(_stat4Card0, 0, new Thickness(0, 0, 8, 0));
            SetStatCardDesktop(_stat4Card1, 1, new Thickness(8, 0, 8, 0));
            SetStatCardDesktop(_stat4Card2, 2, new Thickness(8, 0, 8, 0));
            SetStatCardDesktop(_stat4Card3, 3, new Thickness(8, 0, 0, 0));

            if (_infoGrid != null)
            {
                for (int i = 1; i < _infoGrid.ColumnDefinitions.Count; i++)
                    _infoGrid.ColumnDefinitions[i].Width = GridLength.Star;
            }
            SetInfoCardDesktop(_info4Card0, 0, new Thickness(0, 0, 8, 0));
            SetInfoCardDesktop(_info4Card1, 1, new Thickness(8, 0, 8, 0));
            SetInfoCardDesktop(_info4Card2, 2, new Thickness(8, 0, 8, 0));
            SetInfoCardDesktop(_info4Card3, 3, new Thickness(8, 0, 0, 0));

            if (_stagesTableDesktop != null) _stagesTableDesktop.IsVisible = true;
            if (_stagesCardsMobile != null) _stagesCardsMobile.IsVisible = false;

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

            case "ConfirmInvestmentButton":
                _ = ConfirmInvestmentAsync();
                break;

            case "CancelInvestmentButton":
            case "CancelInvestmentStep1Button":
                _ = CancelInvestmentAsync();
                break;

            case "RefreshInvestmentButton":
                _ = RefreshInvestmentAsync();
                break;

            case "ViewTransactionButton":
                OpenTransactionInBrowser();
                break;
        }
    }

    /// <summary>
    /// Opens the recovery modals overlay based on the RecoveryState ActionKey.
    /// Routes to the correct modal for each of the 5 recovery paths.
    /// </summary>
    private void LaunchRecoveryModals()
    {
        if (DataContext is not InvestmentViewModel investVm) return;

        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        if (shellVm == null) return;

        // Set the appropriate modal visibility based on recovery action key
        switch (investVm.RecoveryActionKey)
        {
            case "unfundedRelease":
                // Recover without penalty — release modal flow
                investVm.ShowReleaseModal = true;
                break;
            case "endOfProject":
                // End of project — claim modal flow
                investVm.ShowClaimModal = true;
                break;
            case "belowThreshold":
                // Below penalty threshold — direct recovery, no penalty popup (#24)
                investVm.ShowRecoveryModal = true;
                break;
            case "recovery":
                // Recover to penalty — recovery confirmation modal
                investVm.ShowRecoveryModal = true;
                break;
            case "penaltyRelease":
                // Recover from penalty — release modal flow
                investVm.ShowReleaseModal = true;
                break;
            default:
                return; // "none" — button shouldn't be visible
        }

        // Create the recovery modals view and wire up DataContext
        var recoveryModals = new RecoveryModalsView
        {
            DataContext = investVm
        };

        shellVm.ShowModal(recoveryModals);
    }

    /// <summary>
    /// Publish investment after founder signs (Gap 1: ConfirmInvestment).
    /// Advances from Step 2 to Step 3 on success.
    /// </summary>
    private async Task ConfirmInvestmentAsync()
    {
        if (DataContext is not InvestmentViewModel investVm) return;
        if (investVm.IsProcessing) return;

        investVm.IsProcessing = true;

        var portfolioVm = App.Services.GetService<PortfolioViewModel>();
        if (portfolioVm != null)
        {
            await portfolioVm.ConfirmInvestmentAsync(investVm);
        }

        investVm.IsProcessing = false;
    }

    /// <summary>
    /// Cancel a pending investment request (Gap 2: CancelInvestmentRequest).
    /// Available at Step 1 (PendingFounderSignatures) and Step 2 (FounderSignaturesReceived).
    /// </summary>
    private async Task CancelInvestmentAsync()
    {
        if (DataContext is not InvestmentViewModel investVm) return;
        if (investVm.IsProcessing) return;

        investVm.IsProcessing = true;

        var portfolioVm = App.Services.GetService<PortfolioViewModel>();
        if (portfolioVm != null)
        {
            await portfolioVm.CancelInvestmentAsync(investVm);
        }

        investVm.IsProcessing = false;
    }

    /// <summary>
    /// Refresh the current investment's data from the SDK, including approval status changes.
    /// Reloads all investments to pick up founder approval, then re-selects this investment
    /// and refreshes its recovery status.
    /// </summary>
    private async Task RefreshInvestmentAsync()
    {
        if (DataContext is not InvestmentViewModel investVm) return;
        if (investVm.IsProcessing) return;

        investVm.IsProcessing = true;

        var portfolioVm = App.Services.GetService<PortfolioViewModel>();
        if (portfolioVm != null)
        {
            var projectId = investVm.ProjectIdentifier;

            // Reload all investments from SDK to pick up approval status changes (#7)
            await portfolioVm.LoadInvestmentsFromSdkAsync();

            // Re-select the same investment (LoadInvestmentsFromSdkAsync recreates VMs)
            var refreshed = portfolioVm.Investments.FirstOrDefault(i => i.ProjectIdentifier == projectId);
            if (refreshed != null)
            {
                portfolioVm.OpenInvestmentDetail(refreshed);
                // OpenInvestmentDetail already triggers LoadRecoveryStatusAsync
            }
        }

        // Note: investVm may be stale now (replaced by refreshed VM), but IsProcessing
        // is set on the old VM which is no longer displayed — this is fine.
        investVm.IsProcessing = false;
    }

    /// <summary>
    /// Open the investment transaction in the system browser via the indexer explorer.
    /// </summary>
    private void OpenTransactionInBrowser()
    {
        if (DataContext is not InvestmentViewModel investVm) return;
        var networkService = App.Services.GetService<INetworkService>();
        if (networkService != null)
        {
            ExplorerHelper.OpenTransaction(networkService, investVm.InvestmentTransactionId);
        }
    }
}
