using System;
using System.Reactive.Linq;
using AngorApp.UI.Controls;
using AngorApp.UI.Controls.Feerate;
using Avalonia.Controls;
using Avalonia.Styling;
using Serilog;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Zafiro.Settings;
using Preset = AngorApp.UI.Controls.Feerate.Preset;

namespace AngorApp.UI.Services;

public partial class UIServices : ReactiveObject
{
    private readonly ISettings<UIPreferences> preferences;

    [Reactive] private bool isDarkThemeEnabled;
    [Reactive] private bool isBitcoinPreferred = true;
    public ILauncherService LauncherService { get; }
    public IDialog Dialog { get; }
    public INotificationService NotificationService { get; }
    public string ProfileName { get; }
    
    public UIServices(ILauncherService launcherService, IDialog dialog, INotificationService notificationService,
        IValidations validations,
        ISettings<UIPreferences> preferences,
        string profileName,
        TopLevel topLevel)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or whitespace.", nameof(profileName));
        }

        this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
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

        this.WhenAnyValue(services => services.IsBitcoinPreferred, services => services.IsDarkThemeEnabled)
            .Skip(1)
            .DistinctUntilChanged()
            .Subscribe(value =>
            {
                var (bitcoinPreferred, darkThemeEnabled) = value;
                var update = this.preferences.Update(current => current with
                {
                    IsBitcoinPreferred = bitcoinPreferred,
                    IsDarkThemeEnabled = darkThemeEnabled
                });
                if (update.IsFailure)
                {
                    Log.Warning("Could not persist UI preferences for profile {Profile}. Reason: {Error}", profileName, update.Error);
                }
            });
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
}
