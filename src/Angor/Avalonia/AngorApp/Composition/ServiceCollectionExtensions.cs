using Angor.UI.Model;
using Angor.UI.Model.Implementation;
using Angor.UI.Model.Implementation.Projects;
using AngorApp.Core;
using AngorApp.Design;
using AngorApp.Sections;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using AngorApp.UI.Services;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Controls.Navigation;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Zafiro.UI;

namespace AngorApp.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUIServices(this IServiceCollection services, Control parent)
    {
        var topLevel = TopLevel.GetTopLevel(parent);

        return services
            .AddSingleton<ILauncherService>(_ => new LauncherService(topLevel!.Launcher))
            .AddSingleton<IDialog, DesktopDialog>()
            .AddSingleton<IActiveWallet, ActiveWallet>()
            .AddSingleton<INotificationService>(_ => new NotificationService(
                new WindowNotificationManager(topLevel)
                {
                    Position = NotificationPosition.BottomRight
                }
            ))
            .AddSingleton<UIServices>(sp => new UIServices(
                sp.GetRequiredService<ILauncherService>(),
                sp.GetRequiredService<IDialog>(),
                sp.GetRequiredService<INotificationService>(),
                sp.GetRequiredService<IActiveWallet>()
            ));
    }

    public static IServiceCollection AddUIModelServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IWalletProvider, WalletProviderDesign>()
            .AddSingleton<IWalletBuilder, WalletBuilderDesign>()
            .AddSingleton<IWalletFactory, WalletFactory>()
            .AddSingleton<IProjectService>(sp =>
            {
                var loggerFactory = LoggerConfig.CreateFactory();
                return new ProjectService(
                    DependencyFactory.GetIndexerService(loggerFactory),
                    DependencyFactory.GetRelayService(loggerFactory)
                );
            });
    }

    private delegate IBrowseSectionViewModel BrowseSectionViewModelFactory(INavigator navigator);

    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        return services
            .AddTransient<BrowseSectionViewModelFactory>(sp =>
                navigator => ActivatorUtilities.CreateInstance<BrowseSectionViewModel>(sp, navigator))
            .AddTransient<NavigationViewModel>(sp =>
                new NavigationViewModel(navigator =>
                    sp.GetRequiredService<BrowseSectionViewModelFactory>()(navigator)
                ))
            .AddSingleton<ISectionsFactory, SectionsFactory>()
            .AddSingleton<Lazy<IMainViewModel>>(sp => new Lazy<IMainViewModel>(sp.GetRequiredService<IMainViewModel>))
            .AddTransient<IHomeSectionViewModel>(sp =>
                new HomeSectionViewModel(
                    sp.GetRequiredService<IActiveWallet>(),
                    sp.GetRequiredService<UIServices>(),
                    () => sp.GetRequiredService<Lazy<IMainViewModel>>().Value
                ))
            .AddTransient<IWalletSectionViewModel, WalletSectionViewModel>()

            // // This registration could be maintained for alternative uses, but for navigation we will use the delegate
            .AddTransient<IBrowseSectionViewModel, BrowseSectionViewModel>()
            .AddTransient<IPortfolioSectionViewModel, PortfolioSectionViewModel>()
            .AddTransient<IFounderSectionViewModel, FounderSectionViewModel>()
            .AddSingleton<IMainViewModel>(sp =>
                new MainViewModel(
                    sp.GetRequiredService<ISectionsFactory>().CreateSections(),
                    sp.GetRequiredService<UIServices>()
                ));
    }
}