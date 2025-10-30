using System.Diagnostics;
using System.IO;
using System.Linq;
using Angor.Shared;
using AngorApp.Core;
using AngorApp.Sections.Shell;
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

public static class UiServices
{
    // Registers UI-level services, dialogs, shell and notifications
    public static IServiceCollection AddUiServices(this IServiceCollection services, Control parent, string profileName)
    {
        var topLevel = TopLevel.GetTopLevel(parent);
        
        Debug.Assert(topLevel != null, "TopLevel cannot be null. Ensure that the parent control is attached to a TopLevel.");
        
        var notificationService = NotificationService(topLevel);
        
        return services
            .AddSettings(
                company: "Angor",
                product: "AngorApp",
                fileName: CreateSettingsFilePath(profileName),
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
            .AddSingleton(sp => ActivatorUtilities.CreateInstance<UIServices>(sp, profileName, topLevel));
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
            
            return navigator.Content.Select(content => new HeaderViewModel(navigator.Back, content, network, uiServices));
        }
        
        return Observable.Return("");
    }

    private static string CreateSettingsFilePath(string profileName)
    {
        var safeProfile = SanitizeProfileName(profileName);
        return Path.Combine("profiles", safeProfile, "ui-settings.json");
    }

    private static string SanitizeProfileName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return "Default";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var trimmed = profileName.Trim();
        var sanitized = new string(trimmed
            .Select(ch => Array.IndexOf(invalidChars, ch) >= 0 ? '_' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "Default" : sanitized;
    }
}
