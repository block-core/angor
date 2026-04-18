using System.Collections.ObjectModel;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Wallet.Application;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using App.UI.Sections.Portfolio;
using App.UI.Shell;
using App.UI.Shared;
using App.UI.Shared.Services;
using Microsoft.Extensions.Logging;

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
    private readonly IDatabaseManagementService _databaseManagementService;
    private readonly ICurrencyService _currencyService;
    private readonly IWalletContext _walletContext;
    private readonly PortfolioViewModel _portfolioViewModel;
    private readonly SignatureStore _signatureStore;
    private readonly ShellViewModel _shellViewModel;
    private readonly ILogger<SettingsViewModel> _logger;

    public event Action<string>? ToastRequested;

    [Reactive] private string networkType;
    [Reactive] private bool isNetworkModalOpen;
    [Reactive] private string? selectedNetworkToSwitch;
    [Reactive] private bool networkChangeConfirmed;

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
        get => _prototypeSettings.IsDarkTheme;
        set
        {
            _prototypeSettings.IsDarkTheme = value;
            this.RaisePropertyChanged();
        }
    }

    public SettingsViewModel(
        INetworkService networkService,
        INetworkConfiguration networkConfig,
        INetworkStorage networkStorage,
        IWalletAppService walletAppService,
        IDatabaseManagementService databaseManagementService,
        PrototypeSettings prototypeSettings,
        ICurrencyService currencyService,
        IWalletContext walletContext,
        PortfolioViewModel portfolioViewModel,
        SignatureStore signatureStore,
        ShellViewModel shellViewModel,
        ILogger<SettingsViewModel> logger)
    {
        _networkService = networkService;
        _networkConfig = networkConfig;
        _networkStorage = networkStorage;
        _walletAppService = walletAppService;
        _databaseManagementService = databaseManagementService;
        _prototypeSettings = prototypeSettings;
        _currencyService = currencyService;
        _walletContext = walletContext;
        _portfolioViewModel = portfolioViewModel;
        _signatureStore = signatureStore;
        _shellViewModel = shellViewModel;
        _logger = logger;

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
    /// Load indexer and relay settings from SDK storage.
    /// </summary>
    private void LoadSettingsFromSdk()
    {
        var settings = _networkStorage.GetSettings();

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

        current.Explorers = new List<SettingsUrl>();

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

    public async Task ConfirmNetworkSwitchAsync()
    {
        if (!NetworkChangeConfirmed || string.IsNullOrEmpty(SelectedNetworkToSwitch)) return;
        if (SelectedNetworkToSwitch == NetworkType) return;

        var newNetwork = SelectedNetworkToSwitch;

        // Delete all document collections (projects, investments, sync data, etc.)
        // This preserves the wallet file but clears all cached/synced data
        var deleteDataResult = await _databaseManagementService.DeleteAllDataAsync();
        if (deleteDataResult.IsFailure)
        {
            _logger.LogError("Failed to clear existing data before network switch: {Error}", deleteDataResult.Error);
            ToastRequested?.Invoke("Failed to switch network. Please try again.");
            return;
        }

        // Persist new network to SDK storage
        _networkStorage.SetNetwork(newNetwork);

        // Clear settings for the new network
        _networkStorage.SetSettings(new SettingsInfo());

        // Switch the runtime network object so Bitcoin operations use the correct parameters
        _networkConfig.SetNetwork(newNetwork switch
        {
            "Mainnet" => new BitcoinMain(),
            "Liquid" => new LiquidMain(),
            _ => new Angornet()
        });

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

        // Rebuild wallet balance data for the new network
        var rebuildResult = await _walletAppService.RebuildAllWalletBalancesAsync();
        if (rebuildResult.IsFailure)
        {
            _logger.LogError("Failed to rebuild wallet balances after network switch: {Error}", rebuildResult.Error);
            ToastRequested?.Invoke("Network switched, but wallet data failed to refresh.");
            return;
        }

        ToastRequested?.Invoke("Network updated successfully.");
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
        try
        {
            await _networkService.CheckServices(true);
            LoadSettingsFromSdk();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh indexer status");
            ToastRequested?.Invoke("Failed to refresh indexer status.");
        }
    }

    /// <summary>
    /// Refresh relay statuses by calling the SDK's CheckServices.
    /// </summary>
    public async Task RefreshRelayStatusAsync()
    {
        try
        {
            await _networkService.CheckServices(true);
            LoadSettingsFromSdk();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh relay status");
            ToastRequested?.Invoke("Failed to refresh relay status.");
        }
    }

    // Wipe data
    public void OpenWipeDataModal() => IsWipeDataModalOpen = true;
    public void CloseWipeDataModal() => IsWipeDataModalOpen = false;

    public async void ConfirmWipeData()
    {
        try
        {
            _logger.LogInformation("Wipe data requested — clearing settings and deleting all wallets");
            IsWipeDataModalOpen = false;

            // Delete all document collections (projects, investments, sync data, etc.)
            var deleteDataResult = await _databaseManagementService.DeleteAllDataAsync();
            if (deleteDataResult.IsFailure)
            {
                _logger.LogError("Failed to delete application data during wipe: {Error}", deleteDataResult.Error);
                ToastRequested?.Invoke("Wipe data failed. Please try again.");
                return;
            }

            // Clear settings
            _networkStorage.SetSettings(new SettingsInfo());
            _networkService.AddSettingsIfNotExist();
            LoadSettingsFromSdk();
            _logger.LogInformation("Network settings cleared and defaults re-initialized");

            _signatureStore.Clear();
            _portfolioViewModel.ResetAfterDataWipe();

            // Delete all wallets and clear wallet context state
            await _walletContext.DeleteAllAsync();

            _shellViewModel.ResetAfterDataWipe();
            _logger.LogInformation("Wipe data completed — live shell state reset");
            ToastRequested?.Invoke("All local data was wiped successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConfirmWipeData failed");
            ToastRequested?.Invoke($"Wipe data failed: {ex.Message}");
        }
    }
}

// ── Table item models ──
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
