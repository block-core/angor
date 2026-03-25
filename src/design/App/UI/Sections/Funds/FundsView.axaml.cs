using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shell;
using App.UI.Shared.Controls;

namespace App.UI.Sections.Funds;

public partial class FundsView : UserControl
{
    /// <summary>Design-time only.</summary>
    public FundsView() => InitializeComponent();

    public FundsView(FundsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // Handle button clicks from EmptyState "Add Wallet", populated "Add Wallet",
        // and WalletCard action buttons (BtnSend, BtnReceive, BtnUtxo)
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Panel visibility is handled by AXAML bindings on HasWallets.
        // The loading spinner panel binds to IsLoading directly.
    }

    /// <summary>
    /// Re-subscribe bindings when the cached view re-enters the logical tree
    /// (same fix applied to PortfolioView and FundersView for view caching).
    /// </summary>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // Reload wallet data when the view re-enters the tree (e.g. after wipe or navigation)
        if (DataContext is FundsViewModel vm)
            _ = vm.LoadWalletsFromSdkAsync();

        InvalidateVisual();
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        // WalletCard action buttons — detect by Name
        switch (btn.Name)
        {
            case "BtnSend":
                OpenSendModal(btn);
                e.Handled = true;
                return;

            case "BtnReceive":
                OpenReceiveModal(btn);
                e.Handled = true;
                return;

            case "BtnUtxo":
                OpenWalletDetailModal(btn);
                e.Handled = true;
                return;
        }

        // EmptyState or seed group "Add Wallet" button
        if (IsAddWalletButton(btn))
        {
            OpenCreateWalletModal();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Find the WalletCard ancestor of a button to extract wallet info.
    /// </summary>
    private static WalletCard? FindParentWalletCard(Button btn)
    {
        return btn.FindAncestorOfType<WalletCard>();
    }

    /// <summary>
    /// Extract wallet info from a WalletCard and open the Send modal.
    /// </summary>
    private void OpenSendModal(Button btn)
    {
        var card = FindParentWalletCard(btn);
        if (card == null) return;

        var shellView = this.FindAncestorOfType<ShellView>();
        if (shellView?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            var modal = new SendFundsModal { DataContext = DataContext };
            modal.SetWallet(
                card.WalletName ?? "Wallet",
                card.WalletType ?? "On-Chain",
                card.Balance ?? "0.0000 BTC",
                card.WalletId);
            shellVm.ShowModal(modal);
        }
    }

    /// <summary>
    /// Extract wallet info from a WalletCard and open the Receive modal.
    /// </summary>
    private void OpenReceiveModal(Button btn)
    {
        var card = FindParentWalletCard(btn);
        if (card == null) return;

        var shellView = this.FindAncestorOfType<ShellView>();
        if (shellView?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            var modal = new ReceiveFundsModal { DataContext = DataContext };
            modal.SetWallet(
                card.WalletName ?? "Wallet",
                card.WalletType ?? "On-Chain",
                card.WalletId);
            shellVm.ShowModal(modal);
        }
    }

    /// <summary>
    /// Extract wallet info from a WalletCard and open the UTXO management modal.
    /// </summary>
    private void OpenWalletDetailModal(Button btn)
    {
        var card = FindParentWalletCard(btn);
        if (card == null) return;

        var shellView = this.FindAncestorOfType<ShellView>();
        if (shellView?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            var modal = new WalletDetailModal { DataContext = DataContext };
            modal.SetWallet(
                card.WalletName ?? "Wallet",
                card.WalletType ?? "On-Chain",
                card.Balance ?? "0.0000 BTC",
                card.WalletId ?? "");
            shellVm.ShowModal(modal);
        }
    }

    /// <summary>
    /// Check if a button is an "Add Wallet" button — either the EmptyState CTA
    /// or one of the green buttons at the bottom of each seed group.
    /// </summary>
    private static bool IsAddWalletButton(Button btn)
    {
        // Check button content for "Add Wallet" text
        if (btn.Content is string text && text.Contains("Add Wallet"))
            return true;

        // Check if the button contains a StackPanel with "Add Wallet" TextBlock
        // (used in both EmptyState and populated seed group buttons)
        if (btn.Content is Avalonia.Controls.StackPanel sp)
        {
            foreach (var child in sp.Children)
            {
                if (child is TextBlock tb && tb.Text == "Add Wallet")
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Open the Create Wallet modal via the shell.
    /// </summary>
    private void OpenCreateWalletModal()
    {
        var shellView = this.FindAncestorOfType<ShellView>();
        if (shellView?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            var modal = new CreateWalletModal { DataContext = DataContext as FundsViewModel };
            shellVm.ShowModal(modal);
        }
    }
}
