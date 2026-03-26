using System.Collections.ObjectModel;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using App.UI.Shared;
using App.UI.Shell;

namespace App.UI.Sections.Settings;

/// <summary>
/// Settings ViewModel — connected to Angor.SDK services.
/// Manages network selection, explorer/indexer/relay configuration,
/// theme, currency display, and data wipe operations.
/// </summary>
public partial class SettingsViewModel : ReactiveObject
{
    private readonly INetworkService _networkService;
    private readonly INetworkConfiguration _networkConfig;
    private readonly INetworkStorage _networkStorage;
    private readonly IWalletAppService _walletAppService;
    private readonly ICurrencyService _currencyService;

    [Reactive] private string networkType;
    [Reactive] private bool isNetworkModalOpen;
    [Reactive] private string? selectedNetworkToSwitch;
    [Reactive] private bool networkChangeConfirmed;

    // Explorer — table-based list
    public ObservableCollection<ExplorerItem> ExplorerLinks { get; } = new();
    [Reactive] private string newExplorerUrl = "";

    // Indexer — table-based list with status
    public ObservableCollection<IndexerItem> IndexerLinks { get; } = new();
    [Reactive] private string newIndexerUrl = "";

    // Nostr Relays — table-based list with name+status
    public ObservableCollection<RelayItem> NostrRelays { get; } = new();
    [Reactive] private string newRelayUrl = "";

    // Currency Display
    [Reactive] private string currencyDisplay;

    /// <summary>Display name for the currency dropdown, e.g. "Bitcoin (BTC)" or "Bitcoin (TBTC)".</summary>
    public string CurrencyDisplayName => $"Bitcoin ({_currencyService.Symbol})";

    /// <summary>Help text below currency dropdown, e.g. "Bitcoin-only application - currency display is fixed to BTC".</summary>
    public string CurrencyHelpText => $"Bitcoin-only application - currency display is fixed to {_currencyService.Symbol}";

    // Wipe data modal
    [Reactive] private bool isWipeDataModalOpen;

    // Debug mode (testnet only)
    [Reactive] private bool isTestnet;

    private readonly PrototypeSettings _prototypeSettings;

    // Prototype settings toggle — delegates to injected PrototypeSettings
    public bool ShowPopulatedApp
    {
        get => _prototypeSettings.ShowPopulatedApp;
        set
        {
            _prototypeSettings.ShowPopulatedApp = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Debug mode toggle — only effective on testnet networks.
    /// Delegates to PrototypeSettings and syncs to INetworkConfiguration.SetDebugMode().
    /// </summary>
    public bool IsDebugMode
    {
        get => _prototypeSettings.IsDebugMode;
        set
        {
            _prototypeSettings.IsDebugMode = value;
            _networkConfig.SetDebugMode(value);
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

    public SettingsViewModel(
        INetworkService networkService,
        INetworkConfiguration networkConfig,
        INetworkStorage networkStorage,
        IWalletAppService walletAppService,
        PrototypeSettings prototypeSettings,
        ICurrencyService currencyService)
    {
        _networkService = networkService;
        _networkConfig = networkConfig;
        _networkStorage = networkStorage;
        _walletAppService = walletAppService;
        _prototypeSettings = prototypeSettings;
        _currencyService = currencyService;

        // Initialize currency display from the network configuration
        currencyDisplay = _currencyService.Symbol;

        // Ensure default settings exist
        _networkService.AddSettingsIfNotExist();

        // Load current network
        networkType = _networkStorage.GetNetwork() ?? "Angornet";

        // Debug mode is only available on testnet networks
        isTestnet = networkType != "Mainnet";

        // Sync initial debug mode state to INetworkConfiguration
        _networkConfig.SetDebugMode(_prototypeSettings.IsDebugMode);

        // Load settings from SDK storage
        LoadSettingsFromSdk();
    }

    /// <summary>
    /// Load explorer, indexer, and relay settings from SDK storage.
    /// </summary>
    private void LoadSettingsFromSdk()
    {
        var settings = _networkStorage.GetSettings();

        ExplorerLinks.Clear();
        foreach (var explorer in settings.Explorers)
        {
            ExplorerLinks.Add(new ExplorerItem
            {
                Url = explorer.Url,
                IsDefault = explorer.IsPrimary
            });
        }

        IndexerLinks.Clear();
        foreach (var indexer in settings.Indexers)
        {
            IndexerLinks.Add(new IndexerItem
            {
                Url = indexer.Url,
                Status = indexer.Status == UrlStatus.Online ? "Online" : "Offline",
                IsDefault = indexer.IsPrimary
            });
        }

        NostrRelays.Clear();
        foreach (var relay in settings.Relays)
        {
            NostrRelays.Add(new RelayItem
            {
                Url = relay.Url,
                Name = relay.Name ?? "Relay",
                Status = relay.Status == UrlStatus.Online ? "Online" : "Offline"
            });
        }
    }

    /// <summary>
    /// Persist current UI settings back to SDK storage.
    /// </summary>
    private void SaveSettingsToSdk()
    {
        var current = _networkStorage.GetSettings();

        current.Explorers = ExplorerLinks.Select(e => new SettingsUrl
        {
            Url = e.Url,
            IsPrimary = e.IsDefault
        }).ToList();

        current.Indexers = IndexerLinks.Select(i => new SettingsUrl
        {
            Url = i.Url,
            IsPrimary = i.IsDefault,
            Status = i.Status == "Online" ? UrlStatus.Online : UrlStatus.Offline
        }).ToList();

        current.Relays = NostrRelays.Select(r => new SettingsUrl
        {
            Url = r.Url,
            Name = r.Name,
            Status = r.Status == "Online" ? UrlStatus.Online : UrlStatus.Offline
        }).ToList();

        _networkStorage.SetSettings(current);
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

        var newNetwork = SelectedNetworkToSwitch;

        // Persist new network to SDK storage
        _networkStorage.SetNetwork(newNetwork);

        // Clear settings for the new network
        _networkStorage.SetSettings(new SettingsInfo());

        // Re-initialize defaults for the new network
        _networkService.AddSettingsIfNotExist();

        NetworkType = newNetwork;
        IsNetworkModalOpen = false;

        // Update testnet state (debug mode only available on testnet)
        IsTestnet = newNetwork != "Mainnet";

        // If switching to mainnet, disable debug mode
        if (!IsTestnet && IsDebugMode)
        {
            IsDebugMode = false;
        }

        // Reload settings from SDK for the new network
        LoadSettingsFromSdk();
    }

    // Explorer list management
    public void AddExplorerLink()
    {
        if (!string.IsNullOrWhiteSpace(NewExplorerUrl))
        {
            ExplorerLinks.Add(new ExplorerItem { Url = NewExplorerUrl.Trim(), IsDefault = false });
            NewExplorerUrl = "";
            SaveSettingsToSdk();
        }
    }

    public void SetDefaultExplorer(ExplorerItem item)
    {
        foreach (var link in ExplorerLinks) link.IsDefault = link == item;
        SaveSettingsToSdk();
    }

    public void RemoveExplorerLink(ExplorerItem item)
    {
        ExplorerLinks.Remove(item);
        SaveSettingsToSdk();
    }

    // Indexer list management
    public void AddIndexerLink()
    {
        if (!string.IsNullOrWhiteSpace(NewIndexerUrl))
        {
            IndexerLinks.Add(new IndexerItem { Url = NewIndexerUrl.Trim(), Status = "Offline", IsDefault = false });
            NewIndexerUrl = "";
            SaveSettingsToSdk();
        }
    }

    public void SetDefaultIndexer(IndexerItem item)
    {
        foreach (var link in IndexerLinks) link.IsDefault = link == item;
        SaveSettingsToSdk();
    }

    public void RemoveIndexerLink(IndexerItem item)
    {
        IndexerLinks.Remove(item);
        SaveSettingsToSdk();
    }

    // Relay list management
    public void AddRelayLink()
    {
        if (!string.IsNullOrWhiteSpace(NewRelayUrl))
        {
            NostrRelays.Add(new RelayItem { Url = NewRelayUrl.Trim(), Name = "Custom", Status = "Online" });
            NewRelayUrl = "";
            SaveSettingsToSdk();
        }
    }

    public void RemoveRelayLink(RelayItem item)
    {
        NostrRelays.Remove(item);
        SaveSettingsToSdk();
    }

    /// <summary>
    /// Refresh indexer statuses by calling the SDK's CheckServices.
    /// </summary>
    public async Task RefreshIndexerStatusAsync()
    {
        await _networkService.CheckServices(true);
        LoadSettingsFromSdk();
    }

    /// <summary>
    /// Refresh relay statuses by calling the SDK's CheckServices.
    /// </summary>
    public async Task RefreshRelayStatusAsync()
    {
        await _networkService.CheckServices(true);
        LoadSettingsFromSdk();
    }

    // Wipe data
    public void OpenWipeDataModal() => IsWipeDataModalOpen = true;
    public void CloseWipeDataModal() => IsWipeDataModalOpen = false;

    public async void ConfirmWipeData()
    {
        IsWipeDataModalOpen = false;

        // Clear settings
        _networkStorage.SetSettings(new SettingsInfo());
        _networkService.AddSettingsIfNotExist();
        LoadSettingsFromSdk();

        // Delete all wallets
        try
        {
            var metadatas = await _walletAppService.GetMetadatas();
            if (metadatas.IsSuccess)
            {
                foreach (var meta in metadatas.Value)
                {
                    await _walletAppService.DeleteWallet(meta.Id);
                }
            }
        }
        catch { }

        // Clear cached views so sections reload with fresh data
        var shellVm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetService<ShellViewModel>(App.Services);
        shellVm?.ClearViewCache();
    }
}

// ── Table item models ──
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

public class RelayItem : ReactiveObject
{
    public string Url { get; set; } = "";
    public string Name { get; set; } = "";

    private string _status = "Online";
    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }
}
