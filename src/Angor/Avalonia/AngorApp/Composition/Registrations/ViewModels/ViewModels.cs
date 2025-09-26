using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Portfolio.Penalties;
using AngorApp.Sections.Portfolio.Recover;
using AngorApp.Sections.Settings;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition.Registrations.ViewModels;

public static class ViewModels
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        return services.AddScoped<IMainViewModel, MainViewModel>()
                .AddTransient<IHomeSectionViewModel, HomeSectionViewModel>()
                .AddTransient<IWalletSectionViewModel, WalletSectionViewModel>()
                .AddTransient<IBrowseSectionViewModel, BrowseSectionViewModel>()
                .AddTransient<IPortfolioSectionViewModel, PortfolioSectionViewModel>()
                .AddTransient<IFounderSectionViewModel, FounderSectionViewModel>()
                .AddTransient<ISettingsSectionViewModel, SettingsSectionViewModel>()
                .AddScoped<IPenaltiesViewModel, PenaltiesViewModel>()
                .AddScoped<IRecoverViewModel, RecoverViewModel>();
    }
}
