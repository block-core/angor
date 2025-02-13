using AngorApp.Sections;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using AngorApp.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Composition.Registrations;

public static class ViewModels
{
    private delegate IBrowseSectionViewModel BrowseSectionViewModelFactory(INavigator navigator);
    
    public static IServiceCollection Register(this IServiceCollection services)
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
                    sp.GetRequiredService<UI.Services.UIServices>(),
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
                    sp.GetRequiredService<UI.Services.UIServices>()
                ));
    }
}