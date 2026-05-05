using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shared;
using App.UI.Shared.Controls;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace App.UI.Sections.Settings;

public partial class SettingsView : UserControl, ISectionView
{
    private readonly ILogger<SettingsView> _logger;
    private SettingsViewModel? _subscribedVm;
    private IDisposable? _layoutSubscription;
    private ScrollableView? _scrollableView;
    private Border? _networkModalCard;
    private Border? _networkModalHeader;
    private Border? _networkModalBody;
    private Border? _networkModalFooter;
    private StackPanel? _networkModalActions;
    private Button? _networkCancelButton;
    private Button? _networkConfirmButton;

    /// <summary>Design-time only.</summary>
    public SettingsView()
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<SettingsView>();
    }

    public SettingsView(SettingsViewModel vm)
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<SettingsView>();
        DataContext = vm;
        DataContextChanged += (_, _) => SubscribeToVmEvents();
        SubscribeToVmEvents();

        _scrollableView = this.GetLogicalDescendants().OfType<ScrollableView>().FirstOrDefault();
        CacheModalControls();

        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact =>
            {
                if (_scrollableView != null)
                    _scrollableView.ContentPadding = isCompact
                        ? new Thickness(16, 16, 16, 96)
                        : new Thickness(24);

                ApplyNetworkModalLayout(isCompact);
            });

        // Mobile perf: detach the settings cards below the fold AND both modals
        // from the visual tree until first render is painted, then re-insert
        // them on an ApplicationIdle-priority dispatch. IsVisible=false would
        // still force layout allocation; detaching skips measure/arrange
        // entirely. ApplicationIdle (rather than Background) ensures the
        // re-insert runs AFTER the first-paint timing window — Background
        // priority would race with (and get charged against) perceived render.
        // Desktop keeps the full list rendered synchronously.
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            var panel = this.FindControl<StackPanel>("SettingsCardsPanel");
            var rootPanel = panel?.GetLogicalAncestors().OfType<Panel>().FirstOrDefault();
            var networkModal = this.FindControl<Border>("NetworkModal");
            var wipeModal = this.FindControl<Border>("WipeDataModal");

            var detachedCards = new List<(int Index, Control Child)>();
            var detachedModals = new List<(int Index, Control Child)>();

            if (panel != null)
            {
                // Keep the first three cards (Network, Theme, Indexer) interactive immediately.
                // Deferring Indexer makes the first tap on its TextBox feel delayed on mobile.
                const int visibleAboveFold = 3;

                for (var i = panel.Children.Count - 1; i >= visibleAboveFold; i--)
                {
                    if (panel.Children[i] is Control c)
                    {
                        detachedCards.Add((i, c));
                        panel.Children.RemoveAt(i);
                    }
                }
            }

            if (rootPanel != null)
            {
                foreach (var modal in new Control?[] { networkModal, wipeModal })
                {
                    if (modal == null) continue;
                    var idx = rootPanel.Children.IndexOf(modal);
                    if (idx >= 0)
                    {
                        detachedModals.Add((idx, modal));
                        rootPanel.Children.RemoveAt(idx);
                    }
                }
            }

            if (detachedCards.Count > 0 || detachedModals.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (panel != null)
                    {
                        foreach (var (idx, c) in detachedCards.OrderBy(t => t.Index))
                        {
                            var safeIdx = Math.Min(idx, panel.Children.Count);
                            panel.Children.Insert(safeIdx, c);
                        }
                    }

                    if (rootPanel != null)
                    {
                        foreach (var (idx, c) in detachedModals.OrderBy(t => t.Index))
                        {
                            var safeIdx = Math.Min(idx, rootPanel.Children.Count);
                            rootPanel.Children.Insert(safeIdx, c);
                        }
                    }
                }, Avalonia.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    public void OnBecameActive() { }
    public void OnBecameInactive() { }

    private void CacheModalControls()
    {
        _networkModalCard = this.FindControl<Border>("NetworkModalCard");
        _networkModalHeader = this.FindControl<Border>("NetworkModalHeader");
        _networkModalBody = this.FindControl<Border>("NetworkModalBody");
        _networkModalFooter = this.FindControl<Border>("NetworkModalFooter");
        _networkModalActions = this.FindControl<StackPanel>("NetworkModalActions");
        _networkCancelButton = this.FindControl<Button>("NetworkCancelButton");
        _networkConfirmButton = this.FindControl<Button>("NetworkConfirmButton");
    }

    private void ApplyNetworkModalLayout(bool isCompact)
    {
        if (_networkModalCard != null)
        {
            _networkModalCard.HorizontalAlignment = isCompact
                ? Avalonia.Layout.HorizontalAlignment.Stretch
                : Avalonia.Layout.HorizontalAlignment.Center;
            _networkModalCard.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            _networkModalCard.MaxWidth = isCompact ? double.PositiveInfinity : 500;
        }

        if (_networkModalHeader != null)
            _networkModalHeader.Padding = isCompact ? new Thickness(20, 18) : new Thickness(24);
        if (_networkModalBody != null)
            _networkModalBody.Padding = isCompact ? new Thickness(20) : new Thickness(24);
        if (_networkModalFooter != null)
            _networkModalFooter.Padding = isCompact ? new Thickness(20, 16) : new Thickness(24, 20);

        if (_networkModalActions != null)
        {
            _networkModalActions.Orientation = isCompact
                ? Avalonia.Layout.Orientation.Vertical
                : Avalonia.Layout.Orientation.Horizontal;
            _networkModalActions.HorizontalAlignment = isCompact
                ? Avalonia.Layout.HorizontalAlignment.Stretch
                : Avalonia.Layout.HorizontalAlignment.Right;
        }

        if (_networkCancelButton != null)
            _networkCancelButton.HorizontalAlignment = isCompact
                ? Avalonia.Layout.HorizontalAlignment.Stretch
                : Avalonia.Layout.HorizontalAlignment.Left;
        if (_networkConfirmButton != null)
            _networkConfirmButton.HorizontalAlignment = isCompact
                ? Avalonia.Layout.HorizontalAlignment.Stretch
                : Avalonia.Layout.HorizontalAlignment.Left;
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

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
        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        shellVm?.ShowToast(message);
    }

    // ── Network ──
    private void OnChangeNetworkClick(object? sender, RoutedEventArgs e)
    {
        Vm?.OpenNetworkModal();
        UpdateNetworkOptionVisuals(Vm?.SelectedNetworkToSwitch);
    }

    private void OnCloseNetworkModal(object? sender, RoutedEventArgs e) =>
        Vm?.CloseNetworkModal();

    private async void OnConfirmNetworkSwitch(object? sender, RoutedEventArgs e)
    {
        try
        {
            await (Vm?.ConfirmNetworkSwitchAsync() ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OnConfirmNetworkSwitch failed");
        }
    }

    private void OnSelectMainnet(object? sender, RoutedEventArgs e) => SelectNetworkOption("Mainnet");
    private void OnSelectTestnet(object? sender, RoutedEventArgs e) => SelectNetworkOption("Testnet");
    private void OnSelectAngornet(object? sender, RoutedEventArgs e) => SelectNetworkOption("Angornet");
    private void OnSelectSignet(object? sender, RoutedEventArgs e) => SelectNetworkOption("Signet");

    private void SelectNetworkOption(string network)
    {
        Vm?.SelectNetworkOption(network);
        UpdateNetworkOptionVisuals(network);
    }

    /// <summary>
    /// Highlights the selected network option button and shows its checkmark.
    /// Uses CSS class toggling per AGENTS.md Rule #9 — no BrushTransition.
    /// </summary>
    private void UpdateNetworkOptionVisuals(string? selected)
    {
        var buttons = new[] { ("Mainnet", BtnMainnet, CheckMainnet),
                              ("Testnet", BtnTestnet, CheckTestnet),
                              ("Angornet", BtnAngornet, CheckAngornet),
                              ("Signet", BtnSignet, CheckSignet) };

        foreach (var (name, btn, check) in buttons)
        {
            var isActive = name == selected;
            btn.Classes.Set("NetworkOptionActive", isActive);
            check.IsVisible = isActive;
        }
    }

    // ── Indexer ──
    private void OnAddIndexer(object? sender, RoutedEventArgs e) =>
        Vm?.AddIndexerLink();

    private void OnRemoveIndexer(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: IndexerItem item })
            Vm?.RemoveIndexerLink(item);
    }

    private void OnToggleDefaultIndexer(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: IndexerItem item })
            Vm?.SetDefaultIndexer(item);
    }

    // ── Relays ──
    private void OnAddRelay(object? sender, RoutedEventArgs e) =>
        Vm?.AddRelayLink();

    private void OnRemoveRelay(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RelayItem item })
            Vm?.RemoveRelayLink(item);
    }

    // ── Refresh Buttons ──
    private void OnRefreshIndexer(object? sender, RoutedEventArgs e)
    {
        if (Vm != null) _ = Vm.RefreshIndexerStatusAsync();
    }

    // ── Wipe Data Modal ──
    private void OnWipeDataClick(object? sender, RoutedEventArgs e) =>
        Vm?.OpenWipeDataModal();

    private void OnCloseWipeDataModal(object? sender, RoutedEventArgs e) =>
        Vm?.CloseWipeDataModal();

    private void OnConfirmWipeData(object? sender, RoutedEventArgs e) =>
        Vm?.ConfirmWipeData();

    private void OnWipeModalBackdropPressed(object? sender, PointerPressedEventArgs e) =>
        Vm?.CloseWipeDataModal();

    // ── Modal Backdrop ──
    private void OnModalBackdropPressed(object? sender, PointerPressedEventArgs e) =>
        Vm?.CloseNetworkModal();

    private void OnModalContentPressed(object? sender, PointerPressedEventArgs e) =>
        e.Handled = true; // Prevent backdrop close when clicking modal content
}
