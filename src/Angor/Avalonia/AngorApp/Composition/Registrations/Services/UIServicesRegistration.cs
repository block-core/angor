using System.IO;
using Angor.Sdk.Common;
using AngorApp.Core;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Dialogs.Implementations;
using Zafiro.Avalonia.Misc;
using Zafiro.Avalonia.Services;
using Zafiro.Settings;
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
            .AddSingleton<ISectionActions, SectionActions>()
            .AddSingleton<IWalletContext, WalletContext>()
            .AddSingleton<IValidations, Validations>()
            .AddSingleton<SharedCommands>()
            .AddSingleton<IStorageProvider>(_ => GetStorageProvider(mainView))
            .AddSingleton<ILauncherService, LauncherService>()
            .AddSingleton<INotificationService, NotificationService>()
            .AddSingleton<IImageValidationService, ImageValidationService>()
            .AddSingleton(sp => ActivatorUtilities.CreateInstance<UIServices>(sp, profileContext.ProfileName, mainView));
    }

    private static IStorageProvider GetStorageProvider(Control mainView)
    {
        var topLevel = TopLevel.GetTopLevel(mainView)
                       ?? Application.Current?.ApplicationLifetime switch
                       {
                           IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
                           ISingleViewApplicationLifetime singleView => TopLevel.GetTopLevel(singleView.MainView),
                           _ => null
                       };

        return topLevel?.StorageProvider ?? throw new InvalidOperationException("Storage provider is not available.");
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
