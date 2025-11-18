using System;
using System.Reactive.Linq;
using Angor.Shared;
using AngorApp.UI.Shared.Controls;
using AngorApp.UI.Shared.Controls.Feerate;
using Avalonia.Controls;
using Avalonia.Styling;
using Blockcore.Networks;
using Reactive.Bindings;
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

    [Reactive] private bool isDarkThemeEnabled;
    [Reactive] private bool isBitcoinPreferred = true;
    [Reactive] private bool isDebugModeEnabled;
    [Reactive] private Network activeNetwork;

    public ILauncherService LauncherService { get; }
    public IDialog Dialog { get; }
    public INotificationService NotificationService { get; }
    public string ProfileName { get; }

    public UIServices(ILauncherService launcherService, IDialog dialog, INotificationService notificationService,
        IValidations validations,
        ISettings<UIPreferences> preferences,
        string profileName,
        TopLevel topLevel,
        INetworkConfiguration networkConfiguration)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or whitespace.", nameof(profileName));
        }

        this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        this.networkConfiguration = networkConfiguration ?? throw new ArgumentNullException(nameof(networkConfiguration));
        ActiveNetwork = networkConfiguration.GetNetwork();

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

        var isDebugModeEffectivelyEnabled =
            this.WhenAnyValue(
                x => x.IsDebugModeEnabled,
                x => x.ActiveNetwork.NetworkType,
                (debugMode, networkType) => networkType == NetworkType.Testnet && debugMode);
        
        IsDebugModeEffectivelyEnabled = new Reactive.Bindings.ReactiveProperty<bool>(isDebugModeEffectivelyEnabled);
    }

    public Reactive.Bindings.ReactiveProperty<bool> IsDebugModeEffectivelyEnabled { get; }

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
}
