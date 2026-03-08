using System;
using System.Reactive.Linq;
using Angor.Sdk.Wallet.Application;
using Angor.Shared;
using AngorApp.UI.Shared.Controls;
using AngorApp.UI.Shared.Controls.Feerate;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Blockcore.Networks;
using Serilog;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Settings;
using Preset = AngorApp.UI.Shared.Controls.Feerate.Preset;

namespace AngorApp.UI.Shared.Services;

public interface IUIServices
{
    bool EnableProductionValidations();
}

public partial class UIServices : ReactiveObject, IUIServices
{
    private readonly ISettings<UIPreferences> preferences;
    private readonly INetworkConfiguration networkConfiguration;
    private readonly IWalletAppService walletAppService;

    [Reactive] private bool isDarkThemeEnabled;
    [Reactive] private bool isBitcoinPreferred = true;
    [Reactive] private bool isDebugModeEnabled;

    public IDialog Dialog { get; }
    public INotificationService NotificationService { get; }
    public string ProfileName { get; }

    public UIServices(IDialog dialog,
        INotificationService notificationService,
        IValidations validations,
        ISettings<UIPreferences> preferences,
        string profileName,
        INetworkConfiguration networkConfiguration,
        IWalletAppService walletAppService,
        Control mainView)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or whitespace.", nameof(profileName));
        }

        this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        this.networkConfiguration = networkConfiguration ?? throw new ArgumentNullException(nameof(networkConfiguration));
        this.walletAppService = walletAppService ?? throw new ArgumentNullException(nameof(walletAppService));

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

            // Sync debug mode to network configuration
            networkConfiguration.SetDebugMode(IsDebugModeEnabled);
        }
        else
        {
            Log.Warning("Could not load UI preferences for profile {Profile}. Reason: {Error}", profileName, loadResult.Error);
        }

        this.WhenAnyValue(services => services.IsDarkThemeEnabled)
            .DistinctUntilChanged()
            .Do(isDarkTheme => Application.Current.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light)
            .Subscribe();

        var topLevel = Observable.FromEventPattern<RoutedEventArgs>(h => mainView.Loaded += h, h => mainView.Loaded -= h)
            .Select(_ => TopLevel.GetTopLevel(mainView))
            .WhereNotNull();

        var property = new Reactive.Bindings.ReactiveProperty<TopLevel?>(topLevel);

        // Propagate preferred unit globally via inheritable attached property
        this.WhenAnyValue(services => services.IsBitcoinPreferred)
            .CombineLatest(topLevel)
            .DistinctUntilChanged()
            .Do(args => AmountOptions.SetIsBitcoinPreferred(args.Second, args.First))
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
    }

    /// <summary>
    /// Determines if production validations should be skipped.
    /// Returns true only when BOTH debug mode is enabled AND the network is testnet.
    /// This allows for more flexible testing in development environments.
    /// </summary>
    /// <returns>True if debug mode is enabled and network is testnet; otherwise false.</returns>
    public virtual bool EnableProductionValidations()
    {
        var isDebugMode = IsDebugModeEnabled;
        var network = networkConfiguration.GetNetwork();
        var isTestnet = network.NetworkType == NetworkType.Testnet;

        return !(isDebugMode && isTestnet);
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

    public async Task<IEnumerable<IFeeratePreset>> GetFeeratePresetsAsync()
    {
        try
        {
            var result = await walletAppService.GetFeeEstimates();
            if (result.IsSuccess)
            {
                var fees = result.Value.OrderByDescending(f => f.FeeRate).ToList();
                var presets = new List<IFeeratePreset>();

                // Map confirmations to named presets (sat/KB → sat/vByte for display)
                foreach (var fee in fees)
                {
                    var satsPerVByte = Math.Max(1, fee.FeeRate / 1000);
                    var name = fee.Confirmations switch
                    {
                        <= 1 => "Priority",
                        <= 6 => "Standard",
                        _ => "Economy"
                    };

                    // Avoid duplicate names
                    if (presets.All(p => p.Name != name))
                    {
                        presets.Add(new Preset(name, new AmountUI(satsPerVByte), null, null));
                    }
                }

                if (presets.Count > 0)
                    return presets;
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Could not fetch fee estimates from indexer: {Error}", ex.Message);
        }

        return FeeratePresets;
    }

    public IValidations Validations { get; }
}
