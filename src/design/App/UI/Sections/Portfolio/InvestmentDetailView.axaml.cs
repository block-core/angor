using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Angor.Shared.Services;
using App.UI.Shell;
using App.UI.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace App.UI.Sections.Portfolio;

public partial class InvestmentDetailView : UserControl
{
    public InvestmentDetailView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
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
