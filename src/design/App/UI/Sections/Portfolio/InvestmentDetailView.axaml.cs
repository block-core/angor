using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shell;
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
                // End of project or below threshold — claim modal flow
                investVm.ShowClaimModal = true;
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
}
