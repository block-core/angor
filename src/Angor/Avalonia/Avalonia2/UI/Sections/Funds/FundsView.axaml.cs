using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia2.UI.Shell;
using Avalonia2.UI.Shared;
using Avalonia2.UI.Shared.Controls;
using ReactiveUI;

namespace Avalonia2.UI.Sections.Funds;

public partial class FundsView : UserControl
{
    private IDisposable? _layoutSubscription;

    // Cached responsive controls
    private Border? _fundsSummaryCard;
    private Grid? _fundsStatsGrid;
    private Border? _fundsStatCard0;
    private Border? _fundsStatCard1;
    private Border? _fundsStatCard2;
    private ScrollableView? _scrollableView;

    /// <summary>Design-time only.</summary>
    public FundsView()
    {
        InitializeComponent();
        CacheControls();
        SubscribeLayout();
    }

    public FundsView(FundsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        CacheControls();
        SubscribeLayout();

        // Handle button clicks from EmptyState "Add Wallet", populated "Add Wallet",
        // and WalletCard action buttons (BtnSend, BtnReceive, BtnUtxo)
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
    }

    private void CacheControls()
    {
        _fundsSummaryCard = this.FindControl<Border>("FundsSummaryCard");
        _fundsStatsGrid = this.FindControl<Grid>("FundsStatsGrid");
        _fundsStatCard0 = this.FindControl<Border>("FundsStatCard0");
        _fundsStatCard1 = this.FindControl<Border>("FundsStatCard1");
        _fundsStatCard2 = this.FindControl<Border>("FundsStatCard2");
        _scrollableView = this.FindControl<ScrollableView>("FundsScrollableView");
    }

    private void SubscribeLayout()
    {
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    /// <summary>
    /// Responsive layout: compact → stats stack single column, reduced padding.
    /// Vue: <=768px → stats-grid repeat(2,1fr) gap 12; <=640px → 1fr.
    /// We use single breakpoint (IsCompact = <=1024px) → 1-col stacked.
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_fundsStatsGrid == null) return;

        if (isCompact)
        {
            // Stats grid: single column stacked
            _fundsStatsGrid.ColumnDefinitions.Clear();
            _fundsStatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _fundsStatsGrid.RowDefinitions.Clear();
            _fundsStatsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _fundsStatsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _fundsStatsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            if (_fundsStatCard0 != null)
            {
                Grid.SetColumn(_fundsStatCard0, 0); Grid.SetRow(_fundsStatCard0, 0);
                _fundsStatCard0.Margin = new Thickness(0, 0, 0, 12);
            }
            if (_fundsStatCard1 != null)
            {
                Grid.SetColumn(_fundsStatCard1, 0); Grid.SetRow(_fundsStatCard1, 1);
                _fundsStatCard1.Margin = new Thickness(0, 0, 0, 12);
            }
            if (_fundsStatCard2 != null)
            {
                Grid.SetColumn(_fundsStatCard2, 0); Grid.SetRow(_fundsStatCard2, 2);
                _fundsStatCard2.Margin = new Thickness(0);
            }

            // Vue: summary card padding 16px on mobile
            if (_fundsSummaryCard != null)
                _fundsSummaryCard.Padding = new Thickness(16);

            // Vue: container padding 16px on mobile, 96px bottom for tab bar clearance
            if (_scrollableView != null)
                _scrollableView.ContentPadding = new Thickness(16, 16, 16, 96);
        }
        else
        {
            // Stats grid: 3 columns
            _fundsStatsGrid.ColumnDefinitions.Clear();
            _fundsStatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _fundsStatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _fundsStatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _fundsStatsGrid.RowDefinitions.Clear();

            if (_fundsStatCard0 != null)
            {
                Grid.SetColumn(_fundsStatCard0, 0); Grid.SetRow(_fundsStatCard0, 0);
                _fundsStatCard0.Margin = new Thickness(0, 0, 8, 0);
            }
            if (_fundsStatCard1 != null)
            {
                Grid.SetColumn(_fundsStatCard1, 1); Grid.SetRow(_fundsStatCard1, 0);
                _fundsStatCard1.Margin = new Thickness(4, 0, 4, 0);
            }
            if (_fundsStatCard2 != null)
            {
                Grid.SetColumn(_fundsStatCard2, 2); Grid.SetRow(_fundsStatCard2, 0);
                _fundsStatCard2.Margin = new Thickness(8, 0, 0, 0);
            }

            // Vue: summary card padding 24px on desktop
            if (_fundsSummaryCard != null)
                _fundsSummaryCard.Padding = new Thickness(24);

            // Vue: container padding 24px on desktop
            if (_scrollableView != null)
                _scrollableView.ContentPadding = new Thickness(24);
        }
    }

    /// <summary>
    /// Re-subscribe bindings when the cached view re-enters the logical tree
    /// (same fix applied to PortfolioView and FundersView for view caching).
    /// </summary>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // Force layout invalidation so bindings re-evaluate when the cached view re-enters.
        // Previous approach used DataContext = null / DataContext = vm which breaks DynamicResource bindings.
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
                card.Balance ?? "0.0000 BTC");
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
                card.WalletType ?? "On-Chain");
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

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }
}
