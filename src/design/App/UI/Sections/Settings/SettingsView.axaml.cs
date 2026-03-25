using Avalonia.Input;
using Avalonia.Interactivity;

namespace App.UI.Sections.Settings;

public partial class SettingsView : UserControl
{
    /// <summary>Design-time only.</summary>
    public SettingsView() => InitializeComponent();

    public SettingsView(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    // ── Network ──
    private void OnChangeNetworkClick(object? sender, RoutedEventArgs e)
    {
        Vm?.OpenNetworkModal();
        UpdateNetworkOptionVisuals(Vm?.SelectedNetworkToSwitch);
    }

    private void OnCloseNetworkModal(object? sender, RoutedEventArgs e) =>
        Vm?.CloseNetworkModal();

    private void OnConfirmNetworkSwitch(object? sender, RoutedEventArgs e) =>
        Vm?.ConfirmNetworkSwitch();

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

    // ── Explorer ──
    private void OnAddExplorer(object? sender, RoutedEventArgs e) =>
        Vm?.AddExplorerLink();

    private void OnRemoveExplorer(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ExplorerItem item })
            Vm?.RemoveExplorerLink(item);
    }

    private void OnToggleDefaultExplorer(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ExplorerItem item })
            Vm?.SetDefaultExplorer(item);
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
