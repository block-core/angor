using System.Collections.ObjectModel;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Sections.Settings;

/// <summary>
/// Settings ViewModel — visual layer only.
/// Vue Settings has 8 sections: Network, Theme (mobile-only, we include it for desktop),
/// Explorer, Indexer, Nostr Relays, Currency Display, Seed Backup, Danger Zone.
/// The backend team will wire up real settings persistence via SDK.
/// </summary>
public partial class SettingsViewModel : ReactiveObject
{
    [Reactive] private string networkType = "Mainnet";
    [Reactive] private bool isNetworkModalOpen;
    [Reactive] private string? selectedNetworkToSwitch;
    [Reactive] private bool networkChangeConfirmed;

    // Explorer — table-based list (Vue: explorerLinks)
    public ObservableCollection<ExplorerItem> ExplorerLinks { get; } = new()
    {
        new ExplorerItem { Url = "https://test.explorer.angor.io", IsDefault = true },
        new ExplorerItem { Url = "https://signet.angor.online", IsDefault = false },
        new ExplorerItem { Url = "https://signet2.angor.online", IsDefault = false },
    };
    [Reactive] private string newExplorerUrl = "";

    // Indexer — table-based list with status (Vue: indexerLinks)
    public ObservableCollection<IndexerItem> IndexerLinks { get; } = new()
    {
        new IndexerItem { Url = "https://test.indexer.angor.io", Status = "Offline", IsDefault = false },
        new IndexerItem { Url = "https://signet.angor.online", Status = "Online", IsDefault = true },
        new IndexerItem { Url = "https://signet2.angor.online", Status = "Offline", IsDefault = false },
    };
    [Reactive] private string newIndexerUrl = "";

    // Nostr Relays — table-based list with name+status (Vue: nostrRelays)
    public ObservableCollection<RelayItem> NostrRelays { get; } = new()
    {
        new RelayItem { Url = "wss://relay.angor.io", Name = "strfry default", Status = "Online" },
        new RelayItem { Url = "wss://relay2.angor.io", Name = "strfry2 default", Status = "Online" },
    };
    [Reactive] private string newRelayUrl = "";

    // Currency Display
    [Reactive] private string currencyDisplay = "BTC";

    // Wipe data modal
    [Reactive] private bool isWipeDataModalOpen;

    // Prototype settings toggle — delegates to SharedViewModels.Prototype
    public bool ShowPopulatedApp
    {
        get => SharedViewModels.Prototype.ShowPopulatedApp;
        set
        {
            SharedViewModels.Prototype.ShowPopulatedApp = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsDarkThemeEnabled
    {
        get => Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        set
        {
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = value
                    ? Avalonia.Styling.ThemeVariant.Dark
                    : Avalonia.Styling.ThemeVariant.Light;
            }
            this.RaisePropertyChanged();
        }
    }

    // Network modal
    public void OpenNetworkModal()
    {
        SelectedNetworkToSwitch = NetworkType;
        NetworkChangeConfirmed = false;
        IsNetworkModalOpen = true;
    }

    public void CloseNetworkModal() => IsNetworkModalOpen = false;

    public void SelectNetworkOption(string network) => SelectedNetworkToSwitch = network;

    public void ConfirmNetworkSwitch()
    {
        if (!NetworkChangeConfirmed || string.IsNullOrEmpty(SelectedNetworkToSwitch)) return;
        if (SelectedNetworkToSwitch == NetworkType) return;
        NetworkType = SelectedNetworkToSwitch;
        IsNetworkModalOpen = false;
    }

    // Explorer list management
    public void AddExplorerLink()
    {
        if (!string.IsNullOrWhiteSpace(NewExplorerUrl))
        {
            ExplorerLinks.Add(new ExplorerItem { Url = NewExplorerUrl.Trim(), IsDefault = false });
            NewExplorerUrl = "";
        }
    }

    public void SetDefaultExplorer(ExplorerItem item)
    {
        foreach (var link in ExplorerLinks) link.IsDefault = link == item;
    }

    public void RemoveExplorerLink(ExplorerItem item) => ExplorerLinks.Remove(item);

    // Indexer list management
    public void AddIndexerLink()
    {
        if (!string.IsNullOrWhiteSpace(NewIndexerUrl))
        {
            IndexerLinks.Add(new IndexerItem { Url = NewIndexerUrl.Trim(), Status = "Offline", IsDefault = false });
            NewIndexerUrl = "";
        }
    }

    public void SetDefaultIndexer(IndexerItem item)
    {
        foreach (var link in IndexerLinks) link.IsDefault = link == item;
    }

    public void RemoveIndexerLink(IndexerItem item) => IndexerLinks.Remove(item);

    // Relay list management
    public void AddRelayLink()
    {
        if (!string.IsNullOrWhiteSpace(NewRelayUrl))
        {
            NostrRelays.Add(new RelayItem { Url = NewRelayUrl.Trim(), Name = "Custom", Status = "Online" });
            NewRelayUrl = "";
        }
    }

    public void RemoveRelayLink(RelayItem item) => NostrRelays.Remove(item);

    // Wipe data
    public void OpenWipeDataModal() => IsWipeDataModalOpen = true;
    public void CloseWipeDataModal() => IsWipeDataModalOpen = false;
    public void ConfirmWipeData()
    {
        // Visual-only: just close the modal. Backend will wire real wipe logic.
        IsWipeDataModalOpen = false;
    }

}

// ── Table item models ──
// These use INotifyPropertyChanged so IsDefault toggling updates the UI.
public class ExplorerItem : ReactiveObject
{
    public string Url { get; set; } = "";

    private bool _isDefault;
    public bool IsDefault
    {
        get => _isDefault;
        set => this.RaiseAndSetIfChanged(ref _isDefault, value);
    }
}

public class IndexerItem : ReactiveObject
{
    public string Url { get; set; } = "";
    public string Status { get; set; } = "Offline";

    private bool _isDefault;
    public bool IsDefault
    {
        get => _isDefault;
        set => this.RaiseAndSetIfChanged(ref _isDefault, value);
    }
}

public class RelayItem
{
    public string Url { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Online";
}
