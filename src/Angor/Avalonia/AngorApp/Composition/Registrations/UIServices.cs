using AngorApp.UI.Services;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Zafiro.UI;

namespace AngorApp.Composition.Registrations;

public static class UIServices
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
            .AddSingleton<IDialog, DesktopDialog>()
            .AddSingleton<IActiveWallet, ActiveWallet>()
            .AddSingleton<INotificationService>(_ => notificationService)
            .AddSingleton<UI.Services.UIServices>();
    }
}