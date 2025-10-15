using System;
using AngorApp.UI.Controls;
using AngorApp.UI.Controls.Feerate;
using Avalonia.Styling;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Preset = AngorApp.UI.Controls.Feerate.Preset;

namespace AngorApp.UI.Services;

public partial class UIServices : ReactiveObject
{
    [Reactive] private bool isDarkThemeEnabled;
    public ILauncherService LauncherService { get; }
    public IDialog Dialog { get; }
    public INotificationService NotificationService { get; }
    public string ProfileName { get; }
    
    public UIServices(ILauncherService launcherService, IDialog dialog, INotificationService notificationService,
        IValidations validations,
        string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or whitespace.", nameof(profileName));
        }

        LauncherService = launcherService;
        Dialog = dialog;
        NotificationService = notificationService;
        Validations = validations;
        ProfileName = profileName;
        this.WhenAnyValue(services => services.IsDarkThemeEnabled)
            .Do(isDarkTheme => Application.Current.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light)
            .Subscribe();
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
