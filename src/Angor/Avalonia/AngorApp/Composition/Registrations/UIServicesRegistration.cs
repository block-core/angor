using System.Diagnostics;
using Angor.Shared;
using AngorApp.Sections.Shell;
using AngorApp.UI;
using AngorApp.UI.Services;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Misc;
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
        
        Debug.Assert(topLevel != null, "TopLevel cannot be null. Ensure that the parent control is attached to a TopLevel.");
        
        var notificationService = NotificationService(topLevel);
        
        return services
            .AddSingleton<ILauncherService>(_ => new LauncherService(topLevel!.Launcher))
            .AddSingleton<IDialog>(new AdornerDialog(() =>
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(parent);
                return adornerLayer!;
            }))
            .AddSingleton<IActiveWallet, ActiveWallet>()
            .AddSingleton(sp => new ShellProperties("Angor", content => GetHeader(content, sp)))
            .AddSingleton<IShell, Shell>()
            .AddSingleton<ISectionActions, SectionActions>()
            .AddSingleton<IWalletRoot, WalletRoot>()
            .AddSingleton<INotificationService>(_ => notificationService)
            .AddSingleton<UIServices>();
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
        if (content is SectionScope scope)
        {
            var config = sp.GetRequiredService<INetworkConfiguration>();
            var network = config.GetNetwork();
            
            return scope.Navigator.Content.Select(o => new HeaderViewModel(scope.Navigator.Back, o, network));
        }
        
        return Observable.Return("");
    }
}