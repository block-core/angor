using System;
using System.Linq;
using Angor.Shared.Services;
using App.UI.Shared;
using App.UI.Shared.Helpers;
using App.UI.Shell;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.UI.Sections.MyProjects.Modals;

/// <summary>
/// Contains the 7 inline modal overlays for ManageProjectView:
/// Claim UTXOs, Confirm Claim, Claim Success,
/// Release Funds, Confirm Release, Release Success,
/// Spent UTXOs.
/// Shares DataContext (ManageProjectViewModel) with parent via inheritance.
/// </summary>
public partial class ManageProjectModalsView : UserControl
{
    private readonly ILogger<ManageProjectModalsView> _logger;
    private ManageProjectViewModel? Vm => DataContext as ManageProjectViewModel;

    public ManageProjectModalsView()
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<ManageProjectModalsView>();
        DataContextChanged += (_, _) => SubscribeToVmEvents();
        SubscribeToVmEvents();

        // ── Claim Flow ──
        WireClick("ClaimModalCloseBtn", () => { if (Vm != null) Vm.ShowClaimModal = false; });
        WireClick("ClaimModalCancelBtn", () => { if (Vm != null) Vm.ShowClaimModal = false; });
        WireClick("ClaimSelectedBtn", OnClaimSelectedClick);

        // ── Password (Claim) ──
        WireClick("PasswordModalCloseBtn", () => { if (Vm != null) Vm.ShowPasswordModal = false; });
        WireClick("PasswordModalCancelBtn", () => { if (Vm != null) Vm.ShowPasswordModal = false; });
        WireClick("ConfirmClaimBtn", OnConfirmClaimClick);

        // ── Success (Claim) ──
        WireClick("GoToFundsBtn", () => { if (Vm != null) Vm.ShowSuccessModal = false; });
        WireClick("SuccessCloseBtn", () => { if (Vm != null) Vm.ShowSuccessModal = false; });

        // ── Release Funds ──
        WireClick("ReleaseFundsModalCloseBtn", () => { if (Vm != null) Vm.ShowReleaseFundsModal = false; });
        WireClick("ReleaseFundsCancelBtn", () => { if (Vm != null) Vm.ShowReleaseFundsModal = false; });
        WireClick("ReleaseFundsConfirmBtn", OnReleaseFundsConfirmClick);

        // ── Password (Release) ──
        WireClick("ReleasePasswordModalCloseBtn", () => { if (Vm != null) Vm.ShowReleaseFundsPasswordModal = false; });
        WireClick("ReleasePasswordCancelBtn", () => { if (Vm != null) Vm.ShowReleaseFundsPasswordModal = false; });
        WireClick("ConfirmReleaseBtn", OnConfirmReleaseClick);

        // ── Success (Release) ──
        WireClick("ReleaseDoneBtn", () => { if (Vm != null) Vm.ShowReleaseFundsSuccessModal = false; });

        // ── Spent UTXOs ──
        WireClick("SpentModalCloseBtn", () => { if (Vm != null) Vm.ShowSpentModal = false; });
        WireClick("SpentModalDoneBtn", () => { if (Vm != null) Vm.ShowSpentModal = false; });

        // ── Modal backdrop clicks (close on click outside the card) ──
        WireBackdropClose("ClaimModalOverlay", () => { if (Vm != null) Vm.ShowClaimModal = false; });
        WireBackdropClose("PasswordModalOverlay", () => { if (Vm != null) Vm.ShowPasswordModal = false; });
        WireBackdropClose("SuccessModalOverlay", () => { if (Vm != null) Vm.ShowSuccessModal = false; });
        WireBackdropClose("ReleaseFundsModalOverlay", () => { if (Vm != null) Vm.ShowReleaseFundsModal = false; });
        WireBackdropClose("ReleasePasswordModalOverlay", () => { if (Vm != null) Vm.ShowReleaseFundsPasswordModal = false; });
        WireBackdropClose("ReleaseSuccessModalOverlay", () => { if (Vm != null) Vm.ShowReleaseFundsSuccessModal = false; });
        WireBackdropClose("SpentModalOverlay", () => { if (Vm != null) Vm.ShowSpentModal = false; });

        // ── UTXO item toggle (click on row toggles selection in claim modal) ──
        var claimList = this.FindControl<ItemsControl>("ClaimUtxoList");
        claimList?.AddHandler(PointerPressedEvent, OnClaimUtxoItemPressed, RoutingStrategies.Tunnel);

        // ── Explorer link clicks (bubbled from any TextBlock with ExplorerTxLink class) ──
        AddHandler(PointerPressedEvent, OnExplorerTxLinkPressed, RoutingStrategies.Bubble);
    }

    private ManageProjectViewModel? _subscribedVm;

    private void SubscribeToVmEvents()
    {
        if (_subscribedVm != null)
            _subscribedVm.ToastRequested -= OnToastRequested;

        _subscribedVm = Vm;

        if (_subscribedVm != null)
            _subscribedVm.ToastRequested += OnToastRequested;
    }

    private void OnToastRequested(string message)
    {
        GetShellVm()?.ShowToast(message);
    }

    // ─────────────────────────────────────────────────────────────────
    //  PUBLIC API — called by parent ManageProjectView
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates and opens the Claim UTXO modal for the given stage index.
    /// Called from ManageProjectView when a Claim button is clicked in a stage card.
    /// </summary>
    public void OpenClaimModal(int stageIndex)
    {
        if (Vm == null || stageIndex < 0 || stageIndex >= Vm.Stages.Count) return;

        Vm.SelectedStageIndex = stageIndex;
        var stage = Vm.Stages[stageIndex];

        // Populate claim UTXO list
        var claimList = this.FindControl<ItemsControl>("ClaimUtxoList");
        if (claimList != null)
        {
            // Reset selection state
            foreach (var tx in stage.AvailableTransactions)
                tx.IsSelected = false;

            claimList.ItemsSource = stage.AvailableTransactions;
        }

        Vm.ShowClaimModal = true;
    }

    /// <summary>
    /// Populates and opens the Spent UTXO modal for the given stage index.
    /// Called from ManageProjectView when a Spent button is clicked in a stage card.
    /// </summary>
    public void OpenSpentModal(int stageIndex)
    {
        if (Vm == null || stageIndex < 0 || stageIndex >= Vm.Stages.Count) return;

        Vm.SelectedStageIndex = stageIndex;
        var stage = Vm.Stages[stageIndex];

        // Populate spent UTXO list
        var spentList = this.FindControl<ItemsControl>("SpentUtxoList");
        if (spentList != null)
            spentList.ItemsSource = stage.SpentTransactions;

        Vm.ShowSpentModal = true;
    }

    /// <summary>
    /// Opens the Release Funds modal with all available UTXOs pre-selected.
    /// Called from ManageProjectView or externally via ManageProjectView.OpenReleaseFundsModal().
    /// </summary>
    public void OpenReleaseFundsModal()
    {
        if (Vm == null) return;

        var releaseList = this.FindControl<ItemsControl>("ReleaseUtxoList");
        if (releaseList != null)
        {
            var allAvailable = Vm.Stages
                .SelectMany(s => s.AvailableTransactions)
                .ToList();

            foreach (var tx in allAvailable)
                tx.IsSelected = true;

            releaseList.ItemsSource = allAvailable;
        }

        Vm.ShowReleaseFundsModal = true;
    }

    // ─────────────────────────────────────────────────────────────────
    //  UTXO ITEM TOGGLE (click row -> toggle IsSelected)
    // ─────────────────────────────────────────────────────────────────

    private void OnClaimUtxoItemPressed(object? sender, PointerPressedEventArgs e)
    {
        // Walk up from the clicked element to find the DataTemplate item's DataContext
        var element = e.Source as Control;
        while (element != null)
        {
            if (element.DataContext is UtxoTransactionViewModel utxo)
            {
                utxo.IsSelected = !utxo.IsSelected;

                // Update checkbox visual — find the UtxoCheckbox border in this item
                UpdateCheckboxVisual(element, utxo.IsSelected);

                e.Handled = true;
                return;
            }
            element = element.Parent as Control;
        }
    }

    private void UpdateCheckboxVisual(Control element, bool selected)
    {
        // Walk up to find the Border named UtxoItemBorder (the row container)
        var container = element;
        while (container != null && !(container is Border b && b.Name == "UtxoItemBorder"))
        {
            container = container.Parent as Control;
        }

        if (container is Border itemBorder)
        {
            // Find the checkbox Border named UtxoCheckbox inside and toggle CSS class
            var checkbox = FindDescendantByName<Border>(itemBorder, "UtxoCheckbox");
            checkbox?.Classes.Set("UtxoChecked", selected);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  CLAIM FLOW
    // ─────────────────────────────────────────────────────────────────

    private async void OnClaimSelectedClick()
    {
        try
        {
            if (Vm == null) return;

            // Calculate total amount from selected UTXOs
            var stage = Vm.SelectedStage;
            if (stage == null) return;

            var selectedTxs = stage.AvailableTransactions.Where(t => t.IsSelected).ToList();
            var selectedAmount = selectedTxs.Sum(t => double.TryParse(t.Amount, out var v) ? v : 0);

            if (selectedAmount <= 0 || selectedTxs.Count == 0) return; // nothing selected

            Vm.ClaimedAmount = selectedAmount.ToString("F8");
            Vm.ShowClaimModal = false;

            // Skip password modal — password is not used (SimplePasswordProvider returns "default-key").
            // Go directly to fee selection and claim.
            var feeRate = await AskForFeeRateAsync();
            if (feeRate == null) return; // User cancelled

            Vm.IsClaiming = true;
            var success = await Vm.ClaimStageFundsAsync(stage.Number, selectedTxs, feeRate.Value);
            Vm.IsClaiming = false;

            if (success)
            {
                Vm.ShowSuccessModal = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OnClaimSelectedClick failed");
            GetShellVm()?.ShowToast($"Claim failed: {ex.Message}");
        }
    }

    private async void OnConfirmClaimClick()
    {
        try
        {
            if (Vm == null) return;

            var stage = Vm.SelectedStage;
            if (stage == null) return;

            var selectedTxs = stage.AvailableTransactions.Where(t => t.IsSelected).ToList();
            if (selectedTxs.Count == 0) return;

            // Show fee selection popup before claiming
            var feeRate = await AskForFeeRateAsync();
            if (feeRate == null) return; // User cancelled

            Vm.IsClaiming = true;
            var confirmText = this.FindControl<TextBlock>("ConfirmClaimText");
            if (confirmText != null) confirmText.Text = "Claiming...";

            var success = await Vm.ClaimStageFundsAsync(stage.Number, selectedTxs, feeRate.Value);

            Vm.IsClaiming = false;
            if (confirmText != null) confirmText.Text = "Confirm";

            if (success)
            {
                Vm.ShowPasswordModal = false;
                Vm.ShowSuccessModal = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OnConfirmClaimClick failed");
            GetShellVm()?.ShowToast($"Claim failed: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  RELEASE FUNDS FLOW
    // ─────────────────────────────────────────────────────────────────

    private async void OnReleaseFundsConfirmClick()
    {
        try
        {
            if (Vm == null) return;

            // Populate release UTXO list with all available transactions from all stages
            var releaseList = this.FindControl<ItemsControl>("ReleaseUtxoList");
            if (releaseList != null)
            {
                var allAvailable = Vm.Stages
                    .SelectMany(s => s.AvailableTransactions)
                    .ToList();

                // Mark all as selected (pre-selected in release flow)
                foreach (var tx in allAvailable)
                    tx.IsSelected = true;

                releaseList.ItemsSource = allAvailable;
            }

            Vm.ShowReleaseFundsModal = false;

            // Skip password modal — password is not used (SimplePasswordProvider returns "default-key").
            // Go directly to release.
            var totalRelease = Vm.Stages
                .SelectMany(s => s.AvailableTransactions)
                .Sum(t => double.TryParse(t.Amount, out var v) ? v : 0);

            Vm.ReleasedAmount = totalRelease.ToString("F8");

            Vm.IsReleasingFunds = true;
            var success = await Vm.ReleaseFundsToInvestorsAsync();
            Vm.IsReleasingFunds = false;

            if (success)
            {
                Vm.ShowReleaseFundsSuccessModal = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OnReleaseFundsConfirmClick failed");
            GetShellVm()?.ShowToast($"Release funds failed: {ex.Message}");
        }
    }

    private async void OnConfirmReleaseClick()
    {
        try
        {
            if (Vm == null) return;

            var totalRelease = Vm.Stages
                .SelectMany(s => s.AvailableTransactions)
                .Sum(t => double.TryParse(t.Amount, out var v) ? v : 0);

            Vm.ReleasedAmount = totalRelease.ToString("F8");

            Vm.IsReleasingFunds = true;
            var confirmText = this.FindControl<TextBlock>("ConfirmReleaseText");
            if (confirmText != null) confirmText.Text = "Releasing...";

            var success = await Vm.ReleaseFundsToInvestorsAsync();

            Vm.IsReleasingFunds = false;
            if (confirmText != null) confirmText.Text = "Confirm";

            if (success)
            {
                Vm.ShowReleaseFundsPasswordModal = false;
                Vm.ShowReleaseFundsSuccessModal = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OnConfirmReleaseClick failed");
            GetShellVm()?.ShowToast($"Release failed: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  FEE SELECTION
    // ─────────────────────────────────────────────────────────────────

    private ShellViewModel? GetShellVm()
    {
        var shellView = this.FindAncestorOfType<ShellView>();
        return shellView?.DataContext as ShellViewModel;
    }

    /// <summary>
    /// Show the reusable FeeSelectionPopup and return the selected fee rate,
    /// or null if the user cancelled. Re-shows this modal on cancel.
    /// </summary>
    private async Task<long?> AskForFeeRateAsync()
    {
        var shellVm = GetShellVm();
        if (shellVm == null) return null;

        var feeRate = await FeeSelectionPopup.ShowAsync(shellVm);

        if (feeRate == null)
        {
            // User cancelled — re-show the manage project modals
            shellVm.ShowModal(this);
        }

        return feeRate;
    }

    // ─────────────────────────────────────────────────────────────────
    //  EXPLORER LINK (click TxId → open in browser)
    // ─────────────────────────────────────────────────────────────────

    private void OnExplorerTxLinkPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not TextBlock tb || !tb.Classes.Contains("ExplorerTxLink"))
            return;

        var txid = tb.Text;
        if (string.IsNullOrWhiteSpace(txid)) return;

        var networkService = App.Services.GetRequiredService<INetworkService>();
        ExplorerHelper.OpenTransaction(networkService, txid);

        e.Handled = true;
    }

    // ─────────────────────────────────────────────────────────────────
    //  UTILITIES
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wires a Click handler on a named Button.
    /// </summary>
    private void WireClick(string buttonName, Action handler)
    {
        var btn = this.FindControl<Button>(buttonName);
        if (btn != null)
            btn.Click += (_, _) => handler();
    }

    /// <summary>
    /// Wires a PointerPressed handler on a named Panel (modal backdrop) that closes only when
    /// the click is directly on the backdrop (not on the modal card inside it).
    /// </summary>
    private void WireBackdropClose(string panelName, Action closeAction)
    {
        var panel = this.FindControl<Panel>(panelName);
        if (panel != null)
        {
            panel.PointerPressed += (_, e) =>
            {
                // Only close if the click is directly on the backdrop Panel itself,
                // not on any child element (the modal card).
                if (e.Source == panel)
                    closeAction();
            };
        }
    }

    /// <summary>
    /// Finds a descendant control by Name within a visual subtree.
    /// Uses GetVisualChildren() extension from Avalonia.VisualTree.
    /// </summary>
    private static T? FindDescendantByName<T>(Avalonia.Visual root, string name) where T : Control
    {
        if (root is T match && match.Name == name)
            return match;

        foreach (var child in root.GetVisualChildren())
        {
            if (child is Avalonia.Visual visualChild)
            {
                var result = FindDescendantByName<T>(visualChild, name);
                if (result != null)
                    return result;
            }
        }

        return null;
    }
}
