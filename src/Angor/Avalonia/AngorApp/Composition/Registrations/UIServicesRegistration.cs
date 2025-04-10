using AngorApp.UI.Services;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Zafiro.UI;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell;

namespace AngorApp.Composition.Registrations;

public static class UIServicesRegistration
{
    public static IServiceCollection Register(this IServiceCollection services, Control parent)
    {
        var topLevel = TopLevel.GetTopLevel(parent);

        var notificationService = new NotificationService(new WindowNotificationManager(topLevel)
        {
            Position = NotificationPosition.BottomRight
        });
        
        return services
            .AddSingleton<ILauncherService>(_ => new LauncherService(topLevel!.Launcher))
            .AddSingleton(DialogService.Create())
            .AddSingleton<IActiveWallet, ActiveWallet>()
            .AddSingleton<IShell, Shell>()
            .AddSingleton<IWalletRoot, WalletRoot>()
            .AddSingleton<INotificationService>(_ => notificationService)
            .AddSingleton<UIServices>();
    }
}