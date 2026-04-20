using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using App.UI.Shared;
using App.UI.Shared.Helpers;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.UI.Sections.Portfolio;

/// <summary>
/// Shell-level modal overlay for the Recovery Funds flow.
/// Contains 4 modal panels: Recovery Confirmation, Claim Penalties,
/// Release Recovery Confirmation, Release Success.
/// DataContext = InvestmentViewModel (set by InvestmentDetailView when opening).
/// Implements IBackdropCloseable so the shell can notify on backdrop clicks.
///
/// Fee selection is delegated to the reusable FeeSelectionPopup:
/// when the user clicks Confirm, this modal hides, the fee popup is shown,
/// and the SDK call proceeds with the selected fee rate.
/// </summary>
public partial class RecoveryModalsView : UserControl, IBackdropCloseable
{
    public RecoveryModalsView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Wire copy button for claim project ID
        // Vue: copyToClipboard(recoveryProjectId) in Claim Penalties modal
        var copyClaimBtn = this.FindControl<Border>("CopyClaimProjectIdBtn");
        if (copyClaimBtn != null)
            copyClaimBtn.PointerPressed += (_, ev) =>
            {
                if (DataContext is InvestmentViewModel vm)
                    ClipboardHelper.CopyToClipboard(this, vm.RecoveryProjectId);
                ev.Handled = true;
            };
    }

    private InvestmentViewModel? Vm => DataContext as InvestmentViewModel;

    /// <summary>
    /// Called by the shell when the backdrop is clicked.
    /// Closes the currently visible modal and resets processing state.
    /// </summary>
    public void OnBackdropCloseRequested()
    {
        CloseAllModals();
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            // ── Modal 1: Recovery Confirmation ──
            case "CloseRecoveryModal":
            case "CancelRecoveryModal":
                CloseAllModals();
                break;

            case "ConfirmRecoveryModal":
                _ = ProcessRecoveryConfirmAsync();
                break;

            // ── Modal 2: Claim Penalties ──
            case "CloseClaimModal":
                CloseAllModals();
                break;

            case "ClaimPenaltyButton":
                _ = ProcessClaimPenaltyAsync();
                break;

            // ── Modal 3: Release Recovery Confirmation ──
            case "CloseReleaseModal":
            case "CancelReleaseModal":
                CloseAllModals();
                break;

            case "ConfirmReleaseModal":
                _ = ProcessReleaseConfirmAsync();
                break;

            // ── Modal 4: Release Success ──
            case "GoToFundsButton":
                CloseAllModals();
                GetShellVm()?.NavigateToFunds();
                break;
        }
    }

    // ── State transitions via real SDK calls ──

    private PortfolioViewModel? GetPortfolioVm() =>
        App.Services.GetService<PortfolioViewModel>();

    /// <summary>
    /// Shows the FeeSelectionPopup and returns the selected fee rate.
    /// Temporarily hides the current recovery modal content to avoid visual overlap.
    /// Returns null if the user cancelled.
    /// </summary>
    private async Task<long?> AskForFeeRateAsync()
    {
        var shellVm = GetShellVm();
        if (shellVm == null) return null;

        // Show the fee popup (replaces us as shell modal content)
        var feeRate = await FeeSelectionPopup.ShowAsync(shellVm);

        if (feeRate == null)
        {
            // User cancelled — re-show the recovery modals
            shellVm.ShowModal(this);
        }

        return feeRate;
    }

    /// <summary>
    /// Routes the recovery confirmation to the correct SDK operation
    /// based on the investment's RecoveryActionKey.
    /// Shows the fee selection popup first.
    /// </summary>
    private async Task ProcessRecoveryConfirmAsync()
    {
        if (Vm == null || Vm.IsProcessing) return;

        Vm.ErrorMessage = null;
        var feeRate = await AskForFeeRateAsync();
        if (feeRate == null) return; // user cancelled

        Vm.IsProcessing = true;

        var portfolioVm = GetPortfolioVm();
        if (portfolioVm != null)
        {
            // Re-show this modal so the user sees the processing spinner
            var shellVm = GetShellVm();
            shellVm?.ShowModal(this);

            var (success, error) = Vm.RecoveryActionKey switch
            {
                "recovery" => await portfolioVm.RecoverFundsAsync(Vm, feeRate.Value),
                "belowThreshold" => await portfolioVm.RecoverFundsAsync(Vm, feeRate.Value),
                "unfundedRelease" => await portfolioVm.ReleaseFundsAsync(Vm, feeRate.Value),
                "endOfProject" => await portfolioVm.ClaimEndOfProjectAsync(Vm, feeRate.Value),
                "penaltyRelease" => await portfolioVm.PenaltyReleaseFundsAsync(Vm, feeRate.Value),
                _ => (false, (string?)"Unknown recovery action.")
            };
            Vm.IsProcessing = false;

            if (success)
            {
                Vm.ShowRecoveryModal = false;
                Vm.ShowSuccessModal = true;
            }
            else
            {
                Vm.ErrorMessage = error ?? "Recovery transaction failed. Please try again later.";
            }
        }
        else
        {
            Vm.IsProcessing = false;
        }
    }

    private async Task ProcessClaimPenaltyAsync()
    {
        if (Vm == null || Vm.IsProcessing) return;

        Vm.ErrorMessage = null;
        var feeRate = await AskForFeeRateAsync();
        if (feeRate == null) return; // user cancelled

        Vm.IsProcessing = true;

        var portfolioVm = GetPortfolioVm();
        if (portfolioVm != null)
        {
            // Re-show this modal so the user sees the processing spinner
            var shellVm = GetShellVm();
            shellVm?.ShowModal(this);

            var (claimSuccess, claimError) = await portfolioVm.ClaimEndOfProjectAsync(Vm, feeRate.Value);
            Vm.IsProcessing = false;

            if (claimSuccess)
            {
                Vm.ShowClaimModal = false;
                Vm.ShowSuccessModal = true;
            }
            else
            {
                Vm.ErrorMessage = claimError ?? "Claim transaction failed. Please try again later.";
            }
        }
        else
        {
            Vm.IsProcessing = false;
        }
    }

    private async Task ProcessReleaseConfirmAsync()
    {
        if (Vm == null || Vm.IsProcessing) return;

        Vm.ErrorMessage = null;
        var feeRate = await AskForFeeRateAsync();
        if (feeRate == null) return; // user cancelled

        Vm.IsProcessing = true;

        var portfolioVm = GetPortfolioVm();
        if (portfolioVm != null)
        {
            // Re-show this modal so the user sees the processing spinner
            var shellVm = GetShellVm();
            shellVm?.ShowModal(this);

            // Route based on action key: could be unfundedRelease or penaltyRelease
            var (releaseSuccess, releaseError) = Vm.RecoveryActionKey switch
            {
                "penaltyRelease" => await portfolioVm.PenaltyReleaseFundsAsync(Vm, feeRate.Value),
                _ => await portfolioVm.ReleaseFundsAsync(Vm, feeRate.Value)
            };
            Vm.IsProcessing = false;

            if (releaseSuccess)
            {
                Vm.ShowReleaseModal = false;
                Vm.ShowSuccessModal = true;
            }
            else
            {
                Vm.ErrorMessage = releaseError ?? "Release transaction failed. Please try again later.";
            }
        }
        else
        {
            Vm.IsProcessing = false;
        }
    }

    // ── Helpers ──

    private void CloseAllModals()
    {
        if (Vm != null)
        {
            Vm.IsProcessing = false;
            Vm.ErrorMessage = null;
            Vm.ShowRecoveryModal = false;
            Vm.ShowClaimModal = false;
            Vm.ShowReleaseModal = false;
            Vm.ShowSuccessModal = false;
        }
        GetShellVm()?.HideModal();
    }

    private ShellViewModel? GetShellVm()
    {
        // Try ancestor first (when we're in the visual tree)
        var shellView = this.FindAncestorOfType<ShellView>();
        if (shellView?.DataContext is ShellViewModel vm1) return vm1;

        // Fallback to service locator (when we've been temporarily removed from tree)
        return App.Services.GetService<ShellViewModel>();
    }
}
