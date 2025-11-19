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
    public static IServiceCollection AddUIServices(this IServiceCollection services, Control mainView, ProfileContext profileContext, IApplicationStorage storage)
    {
        var settingsFilePath = CreateSettingsFilePath(storage, profileContext);
        
        return services
            .AddSettings(
                company: profileContext.AppName,
                product: "AngorApp",
                fileName: settingsFilePath,
                createDefault: UIPreferences.CreateDefault)
            .AddSingleton<IDialog>(new AdornerDialog(() =>
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(mainView);
                return adornerLayer!;
            }))
            .AddSingleton(sp => new ShellProperties("Angor", content => GetHeader(content, sp)))
            .AddSingleton<IShell, Shell>()
            .AddSingleton<ISectionActions, SectionActions>()
            .AddSingleton<IWalletContext, WalletContext>()
            .AddSingleton<IValidations, Validations>()
            .AddSingleton<SharedCommands>()
            .AddSingleton<INotificationService>(_ => NotificationService())
            .AddSingleton<IImageValidationService, ImageValidationService>()
            .AddSingleton(sp => ActivatorUtilities.CreateInstance<UIServices>(sp, profileContext.ProfileName, mainView));
    }
    
    private static NotificationService NotificationService()
    {
        return new NotificationService(() =>
        {
            var managedNotificationManager = new WindowNotificationManager(ApplicationUtils.TopLevel().GetValueOrThrow("No top level window"))
            {
                Position = NotificationPosition.BottomRight,
            };
        
            ApplicationUtils.SafeAreaPadding.BindTo(managedNotificationManager, manager => manager.Margin);
            return managedNotificationManager;
        });
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
