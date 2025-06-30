using Angor.Shared;
using AngorApp.UI.Services;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
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
            .AddSingleton<IDialog>(new AdornerDialog(() =>
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(parent);
                return adornerLayer!;
            }))
            .AddSingleton<IActiveWallet, ActiveWallet>()
            .AddSingleton(sp => new ShellProperties("Angor", o =>
            {
                var config = sp.GetRequiredService<INetworkConfiguration>();
                var network = config.GetNetwork();
                var name = network.Name;
                var networkType = network.NetworkType;
                return Observable.Return($"{name} - {networkType}");
            }))
            .AddSingleton<IShell, Shell>()
            .AddSingleton<IWalletRoot, WalletRoot>()
            .AddSingleton<INotificationService>(_ => notificationService)
            .AddSingleton<UIServices>();
    }
}