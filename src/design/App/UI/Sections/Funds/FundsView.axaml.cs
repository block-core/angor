using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shared;
using App.UI.Shared.Services;
using App.UI.Shell;
using App.UI.Shared.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.UI.Sections.Funds;

public partial class FundsView : UserControl
{
    private readonly ILogger<FundsView> _logger;
    /// <summary>Design-time only.</summary>
    public FundsView()
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<FundsView>();
    }

    public FundsView(FundsViewModel vm)
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<FundsView>();
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
            _ = vm.ReloadWalletsAsync();

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

            case "BtnFaucet":
                _ = RequestTestCoinsAsync(btn);
                e.Handled = true;
                return;

            case "BtnRefresh":
                RefreshWalletBalance(btn);
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

    private ICurrencyService CurrencyService =>
        App.Services.GetRequiredService<ICurrencyService>();

    /// <summary>
    /// Extract wallet info from a WalletCard and open the Send modal.
    /// Uses AvailableSats (confirmed + unconfirmed) for the balance so users can spend
    /// unconfirmed UTXOs. The display Balance property only shows confirmed.
    /// </summary>
    private void OpenSendModal(Button btn)
    {
        var card = FindParentWalletCard(btn);
        if (card == null) return;

        var shellView = this.FindAncestorOfType<ShellView>();
        if (shellView?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            // Get spendable balance (confirmed + unconfirmed) from the WalletInfo DataContext
            var spendableBalance = card.DataContext is WalletInfo walletInfo
                ? walletInfo.FormattedBalanceFull(CurrencyService.Symbol)
                : card.Balance ?? $"0.00000000 {CurrencyService.Symbol}";

            var modal = new SendFundsModal { DataContext = DataContext };
            modal.SetWallet(
                card.WalletName ?? "Wallet",
                card.WalletType ?? "On-Chain",
                spendableBalance,
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
    /// Uses AvailableSats (confirmed + unconfirmed) for the balance, consistent with SendFundsModal.
    /// </summary>
    private void OpenWalletDetailModal(Button btn)
    {
        var card = FindParentWalletCard(btn);
        if (card == null) return;

        var shellView = this.FindAncestorOfType<ShellView>();
        if (shellView?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            var spendableBalance = card.DataContext is WalletInfo walletInfo
                ? walletInfo.FormattedBalanceFull(CurrencyService.Symbol)
                : card.Balance ?? $"0.00000000 {CurrencyService.Symbol}";

            var modal = new WalletDetailModal { DataContext = DataContext };
            modal.SetWallet(
                card.WalletName ?? "Wallet",
                card.WalletType ?? "On-Chain",
                spendableBalance,
                card.WalletId ?? "");
            shellVm.ShowModal(modal);
        }
    }

    /// <summary>
    /// Request testnet coins for a single wallet via its WalletCard.
    /// Awaits the result and shows a toast notification on success or failure.
    /// </summary>
    private async Task RequestTestCoinsAsync(Button btn)
    {
        var card = FindParentWalletCard(btn);
        if (card?.WalletId == null) return;
        if (DataContext is not FundsViewModel vm) return;

        btn.IsEnabled = false;
        try
        {
            var (success, error) = await vm.GetTestCoinsAsync(card.WalletId);

            var shellView = this.FindAncestorOfType<ShellView>();
            if (shellView?.DataContext is ShellViewModel shellVm)
            {
                if (success)
                    shellVm.ShowToast("Testnet coins sent to your wallet. Balance will update shortly.");
                else
                    shellVm.ShowToast($"Faucet failed: {error}");
            }
        }
        catch (Exception ex)
        {
            var shellView = this.FindAncestorOfType<ShellView>();
            if (shellView?.DataContext is ShellViewModel shellVm)
                shellVm.ShowToast($"Error: {ex.Message}");
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    /// <summary>
    /// Refresh balance for a single wallet via its WalletCard.
    /// Sets IsRefreshing on the card to show a spinning icon during the operation.
    /// </summary>
    private async void RefreshWalletBalance(Button btn)
    {
        var card = FindParentWalletCard(btn);
        if (card?.WalletId == null) return;
        if (DataContext is not FundsViewModel vm) return;

        card.IsRefreshing = true;
        try
        {
            await vm.RefreshBalanceAsync(card.WalletId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RefreshWalletBalance failed");
            var shellView = this.FindAncestorOfType<ShellView>();
            if (shellView?.DataContext is ShellViewModel shellVm)
                shellVm.ShowToast($"Failed to refresh balance: {ex.Message}");
        }
        finally
        {
            card.IsRefreshing = false;
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
