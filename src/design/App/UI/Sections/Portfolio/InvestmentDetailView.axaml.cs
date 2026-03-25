using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shell;

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
