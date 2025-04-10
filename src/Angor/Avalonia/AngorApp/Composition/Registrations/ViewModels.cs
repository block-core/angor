using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition.Registrations;

public static class ViewModels
{
    public static IServiceCollection Register(this IServiceCollection services)
    {
        return services
            .AddTransient<Lazy<IMainViewModel>>(sp => new Lazy<IMainViewModel>(sp.GetRequiredService<IMainViewModel>))
            .AddTransient<IHomeSectionViewModel, HomeSectionViewModel>()
            .AddTransient<IWalletSectionViewModel, WalletSectionViewModel>()
            .AddTransient<IBrowseSectionViewModel, BrowseSectionViewModel>()
            .AddTransient<IPortfolioSectionViewModel, PortfolioSectionViewModel>()
            .AddTransient<IFounderSectionViewModel, FounderSectionViewModel>()
            .AddTransient<IMainViewModel, MainViewModel>();
    }
}