using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia2.UI.Shared.Controls;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Sections.MyProjects;

public partial class ManageProjectView : UserControl
{
    private ManageProjectViewModel? Vm => DataContext as ManageProjectViewModel;

    public ManageProjectView()
    {
        InitializeComponent();

        // ── Stage card buttons (inside DataTemplate — use routed event bubbling) ──
        // Buttons tagged with CSS classes "StageClaimBtn" / "StageSpentBtn" bubble Click up
        // to the ItemsControl. We attach a handler on the ItemsControl to catch them.
        var stagesCtrl = this.FindControl<ItemsControl>("StagesItemsControl");
        stagesCtrl?.AddHandler(Button.ClickEvent, OnStageButtonClick, RoutingStrategies.Bubble);

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

        // ── Share button ──
        var shareBtn = this.FindControl<Button>("ShareButton");
        if (shareBtn != null) shareBtn.Click += OnShareClick;

        // ── View Private Keys button (opens shell modal password step) ──
        var viewPKBtn = this.FindControl<Button>("ViewPrivateKeysButton");
        if (viewPKBtn != null) viewPKBtn.Click += OnViewPrivateKeysClick;
    }

    // Track the back button handler to prevent accumulation across SetBackAction calls
    private Button? _backBtn;
    private EventHandler<RoutedEventArgs>? _backClickHandler;

    /// <summary>
    /// Wire the Back button to navigate back to the project list.
    /// Called from MyProjectsView code-behind after the view is created.
    /// Removes any previous handler before adding the new one.
    /// </summary>
    public void SetBackAction(Action backAction)
    {
        _backBtn ??= this.FindControl<Button>("BackButton");
        if (_backBtn == null) return;

        // Remove previous handler to prevent accumulation
        if (_backClickHandler != null)
            _backBtn.Click -= _backClickHandler;

        _backClickHandler = (_, _) => backAction();
        _backBtn.Click += _backClickHandler;
    }

    // ─────────────────────────────────────────────────────────────────
    //  STAGE CARD BUTTONS (routed event from DataTemplate)
    // ─────────────────────────────────────────────────────────────────

    private void OnStageButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn || Vm == null) return;

        if (btn.Classes.Contains("StageClaimBtn"))
        {
            // Tag = stage number (int)
            if (btn.Tag is int stageNum)
            {
                var stageIndex = stageNum - 1; // stages are 1-based
                if (stageIndex >= 0 && stageIndex < Vm.Stages.Count)
                {
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
            }
        }
        else if (btn.Classes.Contains("StageSpentBtn"))
        {
            if (btn.Tag is int stageNum)
            {
                var stageIndex = stageNum - 1;
                if (stageIndex >= 0 && stageIndex < Vm.Stages.Count)
                {
                    Vm.SelectedStageIndex = stageIndex;
                    var stage = Vm.Stages[stageIndex];

                    // Populate spent UTXO list
                    var spentList = this.FindControl<ItemsControl>("SpentUtxoList");
                    if (spentList != null)
                        spentList.ItemsSource = stage.SpentTransactions;

                    Vm.ShowSpentModal = true;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  UTXO ITEM TOGGLE (click row → toggle IsSelected)
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

    private void OnClaimSelectedClick()
    {
        if (Vm == null) return;

        // Calculate total amount from selected UTXOs
        var stage = Vm.SelectedStage;
        if (stage == null) return;

        var selectedAmount = stage.AvailableTransactions
            .Where(t => t.IsSelected)
            .Sum(t => double.TryParse(t.Amount, out var v) ? v : 0);

        if (selectedAmount <= 0) return; // nothing selected

        Vm.ClaimedAmount = selectedAmount.ToString("F8");
        Vm.ShowClaimModal = false;
        Vm.WalletPassword = "";
        Vm.ShowPasswordModal = true;
    }

    private async void OnConfirmClaimClick()
    {
        if (Vm == null) return;

        // Start claiming animation
        Vm.IsClaiming = true;
        var confirmText = this.FindControl<TextBlock>("ConfirmClaimText");
        if (confirmText != null) confirmText.Text = "Claiming...";

        // Simulate network delay (visual-only, no real backend)
        await System.Threading.Tasks.Task.Delay(1500);

        Vm.IsClaiming = false;
        if (confirmText != null) confirmText.Text = "Confirm";

        Vm.ShowPasswordModal = false;
        Vm.ShowSuccessModal = true;
    }

    // ─────────────────────────────────────────────────────────────────
    //  RELEASE FUNDS FLOW
    // ─────────────────────────────────────────────────────────────────

    private void OnReleaseFundsConfirmClick()
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
        Vm.WalletPassword = "";
        Vm.ShowReleaseFundsPasswordModal = true;
    }

    private async void OnConfirmReleaseClick()
    {
        if (Vm == null) return;

        // Calculate total release amount
        var totalRelease = Vm.Stages
            .SelectMany(s => s.AvailableTransactions)
            .Sum(t => double.TryParse(t.Amount, out var v) ? v : 0);

        Vm.ReleasedAmount = totalRelease.ToString("F8");

        // Start releasing animation
        Vm.IsReleasingFunds = true;
        var confirmText = this.FindControl<TextBlock>("ConfirmReleaseText");
        if (confirmText != null) confirmText.Text = "Releasing...";

        // Simulate network delay
        await System.Threading.Tasks.Task.Delay(1500);

        Vm.IsReleasingFunds = false;
        if (confirmText != null) confirmText.Text = "Confirm";

        Vm.ShowReleaseFundsPasswordModal = false;
        Vm.ShowReleaseFundsSuccessModal = true;
        Vm.FundsReleasedToInvestors = true;
    }

    // ─────────────────────────────────────────────────────────────────
    //  SHARE MODAL
    // ─────────────────────────────────────────────────────────────────

    private void OnShareClick(object? sender, RoutedEventArgs e)
    {
        if (Vm?.Project == null) return;

        var shell = this.FindAncestorOfType<ShellView>();
        if (shell?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            var modal = new ShareModal(Vm.Project.Name, Vm.Project.Description);
            shellVm.ShowModal(modal);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  VIEW PRIVATE KEYS (shell modal)
    // ─────────────────────────────────────────────────────────────────

    private void OnViewPrivateKeysClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null) return;

        var shell = this.FindAncestorOfType<ShellView>();
        if (shell?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            var modal = new PrivateKeysPasswordModal(
                Vm.ProjectId, Vm.FounderKey, Vm.RecoveryKey,
                Vm.NostrNpub, Vm.Nip05, Vm.NostrNsec, Vm.NostrHex);
            shellVm.ShowModal(modal);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  HELPER: Open Release Funds modal (from stage or global button)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the Release Funds modal. Call this from an external "Release Funds Back to Investors" button.
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
