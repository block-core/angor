using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Angor.Contexts.CrossCutting;
using Angor.Shared;
using AngorApp.Core;
using AngorApp.UI.Sections.Shell;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Dialogs.Implementations;
using Zafiro.Avalonia.Misc;
using Zafiro.Avalonia.Services;
using Zafiro.Settings;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell;

namespace AngorApp.Composition.Registrations.Services;

public static class UIServicesRegistration
{
    // Registers UI-level services, dialogs, shell and notifications
    public static IServiceCollection AddUiServices(this IServiceCollection services, Control parent, ProfileContext profileContext, IApplicationStorage storage)
    {
        var topLevel = TopLevel.GetTopLevel(parent);
        
        Debug.Assert(topLevel != null, "TopLevel cannot be null. Ensure that the parent control is attached to a TopLevel.");
        
        var notificationService = NotificationService(topLevel);
        var settingsFilePath = CreateSettingsFilePath(storage, profileContext);
        
        return services
            .AddSettings(
                company: profileContext.AppName,
                product: "AngorApp",
                fileName: settingsFilePath,
                createDefault: UIPreferences.CreateDefault)
            .AddSingleton<ILauncherService>(_ => new LauncherService(topLevel!.Launcher))
            .AddSingleton<IDialog>(new AdornerDialog(() =>
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(parent);
                return adornerLayer!;
            }))
            .AddSingleton(sp => new ShellProperties("Angor", content => GetHeader(content, sp)))
            .AddSingleton<IShell, Shell>()
            .AddSingleton<ISectionActions, SectionActions>()
            .AddSingleton<IWalletContext, WalletContext>()
            .AddSingleton<IValidations, Validations>()
            .AddSingleton<SharedCommands>()
            .AddSingleton<INotificationService>(_ => notificationService)
            .AddSingleton<IImageValidationService, ImageValidationService>()
            .AddSingleton(sp => ActivatorUtilities.CreateInstance<UIServices>(sp, profileContext.ProfileName, topLevel));
    }
    
    private static NotificationService NotificationService(TopLevel topLevel)
    {
        var managedNotificationManager = new WindowNotificationManager(topLevel)
        {
            Position = NotificationPosition.BottomRight,
        };
        
        ApplicationUtils.SafeAreaPadding.BindTo(managedNotificationManager, manager => manager.Margin);
        
        return new NotificationService(managedNotificationManager);
    }

    private static IObservable<object?> GetHeader(object content, IServiceProvider sp)
    {
        if (content is INavigator navigator)
        {
            var config = sp.GetRequiredService<INetworkConfiguration>();
            var network = config.GetNetwork();
            var uiServices = sp.GetRequiredService<UIServices>();
            
            return navigator.Content.Select(content => new HeaderViewModel(navigator.Back, content, network, uiServices, config));
        }
        
        return Observable.Return("");
    }

    private static string CreateSettingsFilePath(IApplicationStorage storage, ProfileContext profileContext)
    {
        try
        {
            return storage.GetProfileFilePath(profileContext.AppName, profileContext.ProfileName, "ui-settings.json");
        }
        catch (Exception)
        {
            var fallbackRoot = Path.Combine(AppContext.BaseDirectory, "Profiles");
            Directory.CreateDirectory(fallbackRoot);
            var profileDirectory = Path.Combine(fallbackRoot, profileContext.ProfileName);
            Directory.CreateDirectory(profileDirectory);
            return Path.Combine(profileDirectory, "ui-settings.json");
        }
    }
}
