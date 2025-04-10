using Angor.Contexts.Wallet.Domain;
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
            .AddScoped<INavigator, Navigator>()
            .AddTransient<ISectionsFactory, SectionsFactory>()
            .AddTransient<Lazy<IMainViewModel>>(sp => new Lazy<IMainViewModel>(sp.GetRequiredService<IMainViewModel>))
            .AddTransient<IHomeSectionViewModel, HomeSectionViewModel>()
            .AddTransient<IWalletSectionViewModel, WalletSectionViewModel>()

            // // This registration could be maintained for alternative uses, but for navigation we will use the delegate
            .AddTransient<IBrowseSectionViewModel, BrowseSectionViewModel>()
            .AddTransient<IPortfolioSectionViewModel, PortfolioSectionViewModel>()
            .AddTransient<IFounderSectionViewModel, FounderSectionViewModel>()
            .AddTransient<IMainViewModel>(sp =>
                new MainViewModel(
                    sp.GetRequiredService<ISectionsFactory>().CreateSections(),
                    sp.GetRequiredService<UI.Services.UIServices>()
                ));
    }
}