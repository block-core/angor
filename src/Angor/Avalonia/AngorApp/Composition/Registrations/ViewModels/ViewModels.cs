using AngorApp.Core.Factories;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Browse.ProjectLookup;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Founder.ProjectDetails;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Portfolio.Penalties;
using AngorApp.Sections.Portfolio.Recover;
using AngorApp.Sections.Settings;
using AngorApp.Sections.Shell;
using AngorApp.UI.Controls.Common.FoundedProjectOptions;
using Microsoft.Extensions.DependencyInjection;
using IWalletSectionViewModel = AngorApp.Sections.Wallet.Main.IWalletSectionViewModel;
using WalletSectionViewModel = AngorApp.Sections.Wallet.Main.WalletSectionViewModel;

namespace AngorApp.Composition.Registrations.ViewModels;

public static class ViewModels
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        return services.AddScoped<IMainViewModel, MainViewModel>()
                .AddScoped<IProjectDetailsViewModelFactory, ProjectDetailsViewModelFactory>()
                .AddScoped<IProjectViewModelFactory, ProjectViewModelFactory>()
                .AddScoped<IProjectLookupViewModelFactory, ProjectLookupViewModelFactory>()
                .AddScoped<IProjectInvestCommandFactory, ProjectInvestCommandFactory>()
                .AddScoped<IFoundedProjectOptionsViewModelFactory, FoundedProjectOptionsViewModelFactory>()
                .AddScoped<IFounderProjectDetailsViewModelFactory, FounderProjectDetailsViewModelFactory>()
                .AddScoped<IFounderProjectViewModelFactory, FounderProjectViewModelFactory>()
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
