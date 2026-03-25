using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
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
/// </summary>
public partial class RecoveryModalsView : UserControl, IBackdropCloseable
{
    public RecoveryModalsView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
        AddHandler(Border.PointerPressedEvent, OnBorderPressed, RoutingStrategies.Bubble);

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

    private static long GetFeeRate(string priority) => priority switch
    {
        "priority" => 50,
        "economy" => 5,
        _ => 20 // "standard"
    };

    private PortfolioViewModel? GetPortfolioVm() =>
        App.Services.GetService<PortfolioViewModel>();

    private async Task ProcessRecoveryConfirmAsync()
    {
        if (Vm == null || Vm.IsProcessing) return;
        Vm.IsProcessing = true;

        var portfolioVm = GetPortfolioVm();
        if (portfolioVm != null)
        {
            var feeRate = GetFeeRate(Vm.SelectedFeePriority);
            var success = await portfolioVm.RecoverFundsAsync(Vm, feeRate);
            Vm.IsProcessing = false;

            if (success)
            {
                Vm.ShowRecoveryModal = false;
                GetShellVm()?.HideModal();
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
        Vm.IsProcessing = true;

        var portfolioVm = GetPortfolioVm();
        if (portfolioVm != null)
        {
            var feeRate = GetFeeRate(Vm.SelectedFeePriority);
            var success = await portfolioVm.ClaimEndOfProjectAsync(Vm, feeRate);
            Vm.IsProcessing = false;

            if (success)
            {
                Vm.PenaltyState = "canRelease";
                Vm.ShowClaimModal = false;
                GetShellVm()?.HideModal();
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
        Vm.IsProcessing = true;

        var portfolioVm = GetPortfolioVm();
        if (portfolioVm != null)
        {
            var feeRate = GetFeeRate(Vm.SelectedFeePriority);
            var success = await portfolioVm.ReleaseFundsAsync(Vm, feeRate);
            Vm.IsProcessing = false;

            if (success)
            {
                Vm.ShowReleaseModal = false;
                Vm.ShowSuccessModal = true;
                // Don't HideModal — stay in modal overlay showing success
            }
        }
        else
        {
            Vm.IsProcessing = false;
        }
    }

    // ── Fee priority border selection handling ──

    private void OnBorderPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Control;
        Border? found = null;
        string? foundName = null;

        // Walk up the tree to find a named fee border
        while (source != null)
        {
            if (source is Border b && !string.IsNullOrEmpty(b.Name))
            {
                var name = b.Name;
                if (IsFeeBorderName(name))
                {
                    found = b;
                    foundName = name;
                    break;
                }
            }
            source = source.Parent as Control;
        }

        if (found == null || foundName == null) return;

        // Determine which set (Modal 1 or Modal 3) and which option
        if (foundName.StartsWith("ReleaseFee"))
        {
            // Modal 3 fee buttons
            SelectReleaseFee(foundName);
        }
        else
        {
            // Modal 1 fee buttons
            SelectRecoveryFee(foundName);
        }

        e.Handled = true;
    }

    private static bool IsFeeBorderName(string name) =>
        name is "FeePriority" or "FeeStandard" or "FeeEconomy"
            or "ReleaseFeePriority" or "ReleaseFeeStandard" or "ReleaseFeeEconomy";

    // ── Modal 1 fee selection ──

    private void SelectRecoveryFee(string selectedName)
    {
        var priority = this.FindControl<Border>("FeePriority");
        var standard = this.FindControl<Border>("FeeStandard");
        var economy = this.FindControl<Border>("FeeEconomy");

        SetFeeSelection(priority, selectedName == "FeePriority",
            "FeePriorityLabel", "FeePriorityDesc", "FeePriorityRate");
        SetFeeSelection(standard, selectedName == "FeeStandard",
            "FeeStandardLabel", "FeeStandardDesc", "FeeStandardRate");
        SetFeeSelection(economy, selectedName == "FeeEconomy",
            "FeeEconomyLabel", "FeeEconomyDesc", "FeeEconomyRate");

        if (Vm != null)
        {
            Vm.SelectedFeePriority = selectedName switch
            {
                "FeePriority" => "priority",
                "FeeStandard" => "standard",
                "FeeEconomy" => "economy",
                _ => "standard"
            };
        }
    }

    // ── Modal 3 fee selection ──

    private void SelectReleaseFee(string selectedName)
    {
        var priority = this.FindControl<Border>("ReleaseFeePriority");
        var standard = this.FindControl<Border>("ReleaseFeeStandard");
        var economy = this.FindControl<Border>("ReleaseFeeEconomy");

        SetFeeSelection(priority, selectedName == "ReleaseFeePriority",
            "ReleaseFeePriorityLabel", "ReleaseFeePriorityDesc", null);
        SetFeeSelection(standard, selectedName == "ReleaseFeeStandard",
            "ReleaseFeeStandardLabel", "ReleaseFeeStandardDesc", null);
        SetFeeSelection(economy, selectedName == "ReleaseFeeEconomy",
            "ReleaseFeeEconomyLabel", "ReleaseFeeEconomyDesc", null);
    }

    /// <summary>
    /// Toggles FeeSelected CSS class and updates text foreground colors.
    /// Per Rule #9: no BrushTransition — instant state changes only.
    /// </summary>
    private void SetFeeSelection(Border? border, bool isSelected,
        string labelName, string descName, string? rateName)
    {
        if (border == null) return;

        border.Classes.Set("FeeSelected", isSelected);

        var selectedFg = Brushes.White;
        var unselectedFg = this.TryFindResource("RecoveryFeeUnselectedText", out var res) && res is IBrush brush
            ? brush
            : Brushes.Gray;

        var fg = isSelected ? selectedFg : unselectedFg;

        SetTextForeground(labelName, fg);
        SetTextForeground(descName, fg);
        if (rateName != null) SetTextForeground(rateName, fg);
    }

    private void SetTextForeground(string name, IBrush fg)
    {
        var tb = this.FindControl<TextBlock>(name);
        if (tb != null) tb.Foreground = fg;
    }

    // ── Helpers ──

    private void CloseAllModals()
    {
        if (Vm != null)
        {
            Vm.IsProcessing = false;
            Vm.ShowRecoveryModal = false;
            Vm.ShowClaimModal = false;
            Vm.ShowReleaseModal = false;
            Vm.ShowSuccessModal = false;
        }
        GetShellVm()?.HideModal();
    }

    private ShellViewModel? GetShellVm()
    {
        var shellView = this.FindAncestorOfType<ShellView>();
        return shellView?.DataContext as ShellViewModel;
    }
}
