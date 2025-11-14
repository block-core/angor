using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Angor.Shared;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using AngorApp.UI.Shared.Controls;
using AngorApp.UI.Shared.Controls.Feerate;
using Avalonia.Styling;
using Blockcore.Networks;
using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Zafiro.Settings;
using Preset = AngorApp.UI.Shared.Controls.Feerate.Preset;

namespace AngorApp.UI.Shared.Services;

public partial class UIServices : ReactiveObject
{
    private readonly ISettings<UIPreferences> preferences;
    private readonly INetworkConfiguration networkConfiguration;
    private readonly INetworkStorage networkStorage;
    private readonly INetworkService networkService;
    private static readonly IReadOnlyList<string> networkOptions = new[] { "Angornet", "Mainnet" };

    [Reactive] private bool isDarkThemeEnabled;
    [Reactive] private bool isBitcoinPreferred = true;
    [Reactive] private bool isDebugModeEnabled;
    [Reactive] private Network currentNetwork;

    public ILauncherService LauncherService { get; }
    public IDialog Dialog { get; }
    public INotificationService NotificationService { get; }
    public string ProfileName { get; }

    public UIServices(ILauncherService launcherService, IDialog dialog, INotificationService notificationService,
        IValidations validations,
        ISettings<UIPreferences> preferences,
        string profileName,
        TopLevel topLevel,
        INetworkConfiguration networkConfiguration,
        INetworkStorage networkStorage,
        INetworkService networkService)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or whitespace.", nameof(profileName));
        }

        this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        this.networkConfiguration = networkConfiguration ?? throw new ArgumentNullException(nameof(networkConfiguration));
        this.networkStorage = networkStorage ?? throw new ArgumentNullException(nameof(networkStorage));
        this.networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));

        LauncherService = launcherService;
        Dialog = dialog;
        NotificationService = notificationService;
        Validations = validations;
        ProfileName = profileName;

        var loadResult = this.preferences.Get();
        if (loadResult.IsSuccess)
        {
            IsBitcoinPreferred = loadResult.Value.IsBitcoinPreferred;
            IsDarkThemeEnabled = loadResult.Value.IsDarkThemeEnabled;
            IsDebugModeEnabled = loadResult.Value.IsDebugModeEnabled;

            var preferredNetwork = loadResult.Value.SelectedNetwork;
            if (!string.IsNullOrWhiteSpace(preferredNetwork))
            {
                networkStorage.SetNetwork(preferredNetwork);
                var resolved = ResolveNetwork(preferredNetwork);
                networkConfiguration.SetNetwork(resolved);
                CurrentNetwork = resolved;
            }
            else
            {
                CurrentNetwork = EnsureCurrentNetwork();
            }

            // Sync debug mode to network configuration
            networkConfiguration.SetDebugMode(IsDebugModeEnabled);
        }
        else
        {
            Log.Warning("Could not load UI preferences for profile {Profile}. Reason: {Error}", profileName, loadResult.Error);
            CurrentNetwork = EnsureCurrentNetwork();
        }

        this.WhenAnyValue(services => services.IsDarkThemeEnabled)
                  .DistinctUntilChanged()
                  .Do(isDarkTheme => Application.Current.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light)
                  .Subscribe();

        // Propagate preferred unit globally via inheritable attached property
        this.WhenAnyValue(services => services.IsBitcoinPreferred)
                  .DistinctUntilChanged()
                  .Do(isBtc => AmountOptions.SetIsBitcoinPreferred(topLevel, isBtc))
                  .Subscribe();

        // Sync debug mode changes to network configuration
        this.WhenAnyValue(services => services.IsDebugModeEnabled)
            .DistinctUntilChanged()
            .Do(isDebug => networkConfiguration.SetDebugMode(isDebug))
            .Subscribe();

        this.WhenAnyValue(
                services => services.IsBitcoinPreferred,
                services => services.IsDarkThemeEnabled,
                services => services.IsDebugModeEnabled)
                .Skip(1)
                .DistinctUntilChanged()
                    .Subscribe(tuple =>
                    {
                        var (bitcoinPreferred, darkThemeEnabled, debugModeEnabled) = tuple;
                        var update = this.preferences.Update(current => current with
                        {
                            IsBitcoinPreferred = bitcoinPreferred,
                            IsDarkThemeEnabled = darkThemeEnabled,
                            IsDebugModeEnabled = debugModeEnabled
                        });
                        if (update.IsFailure)
                        {
                            Log.Warning("Could not persist UI preferences for profile {Profile}. Reason: {Error}", profileName, update.Error);
                        }
                    });

        var initialValue = this.WhenAnyValue(services => services.IsDebugModeEnabled, services => services.CurrentNetwork, GetShouldSkipProductionValidations);

        ShouldSkipProductionValidations = new Reactive.Bindings.ReactiveProperty<bool>(initialValue);
    }

    public Reactive.Bindings.ReactiveProperty<bool> ShouldSkipProductionValidations { get; }

    public IReadOnlyList<string> NetworkOptions => networkOptions;

    public string CurrentNetworkName => MapNetworkToDisplayName(CurrentNetwork);

    public Task<Result> ApplyNetworkSelectionAsync(string networkName)
    {
        if (string.IsNullOrWhiteSpace(networkName))
        {
            return Task.FromResult(Result.Failure("Network name cannot be null or empty."));
        }

        if (string.Equals(CurrentNetworkName, networkName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result.Success());
        }

        var previousNetwork = CurrentNetwork;
        var previousNetworkName = CurrentNetworkName;

        try
        {
            var targetNetwork = ResolveNetwork(networkName);
            networkConfiguration.SetNetwork(targetNetwork);
            CurrentNetwork = targetNetwork;
            networkStorage.SetNetwork(networkName);
            networkService.AddSettingsIfNotExist();
            PersistNetworkPreference(networkName);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            networkConfiguration.SetNetwork(previousNetwork);
            CurrentNetwork = previousNetwork;
            networkStorage.SetNetwork(previousNetworkName);
            return Task.FromResult(Result.Failure(ex.Message));
        }
    }
    
    private bool GetShouldSkipProductionValidations(bool isDebugModeEnabled, Network currentNetwork)
    {
        var isTestnet = currentNetwork.NetworkType == NetworkType.Testnet;
        return isDebugModeEnabled && isTestnet;
    }

    public IEnumerable<IFeeratePreset> FeeratePresets
    {
        get
        {
            return new[]
            {
                new Preset("Economy", new AmountUI(2), null, null),
                new Preset("Standard", new AmountUI(12), null, null),
                new Preset("Priority", new AmountUI(20), null, null),
            };
        }
    }

    public IValidations Validations { get; }

    private Network EnsureCurrentNetwork()
    {
        try
        {
            return networkConfiguration.GetNetwork();
        }
        catch (ApplicationException)
        {
            var storedNetwork = networkStorage.GetNetwork();
            var resolvedNetwork = ResolveNetwork(storedNetwork);
            networkConfiguration.SetNetwork(resolvedNetwork);
            return resolvedNetwork;
        }
    }

    private static Network ResolveNetwork(string networkName) =>
        networkName switch
        {
            "Main" => new BitcoinMain(),
            "Mainnet" => new BitcoinMain(),
            _ => new Angornet(),
        };

    private static string MapNetworkToDisplayName(Network? network)
    {
        if (network is null)
        {
            return "Angornet";
        }

        return network.Name switch
        {
            "Main" => "Mainnet",
            "Mainnet" => "Mainnet",
            "Angornet" => "Angornet",
            _ => network.Name
        };
    }

    private void PersistNetworkPreference(string networkName)
    {
        var update = this.preferences.Update(current => current with
        {
            SelectedNetwork = networkName
        });

        if (update.IsFailure)
        {
            Log.Warning("Could not persist network preference for profile {Profile}. Reason: {Error}", ProfileName, update.Error);
        }
    }
}
