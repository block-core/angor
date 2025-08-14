using Angor.Contexts.Funding.Projects.Application.Dtos;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Founder.CreateProject;
using AngorApp.Sections.Founder.ProjectDetails;
using AngorApp.Sections.Founder.ProjectDetails.ManageFunds;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio.Penalties;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using AngorApp.Sections.Portfolio.Recover;
using AngorApp.Sections.Settings;
using Microsoft.Extensions.DependencyInjection;
using FounderProjectDetailsViewModel = AngorApp.Sections.Founder.ProjectDetails.FounderProjectDetailsViewModel;

namespace AngorApp.Composition.Registrations;

public static class ViewModels
{
    public static IServiceCollection Register(this IServiceCollection services)
    {
        return services.AddScoped<IMainViewModel, MainViewModel>()
                .AddTransient<IHomeSectionViewModel, HomeSectionViewModel>()
                .AddTransient<IWalletSectionViewModel, WalletSectionViewModel>()
                .AddTransient<IBrowseSectionViewModel, BrowseSectionViewModel>()
                .AddTransient<IPortfolioSectionViewModel, PortfolioSectionViewModel>()
                .AddTransient<IFounderSectionViewModel, FounderSectionViewModel>()
                .AddTransient<ISettingsSectionViewModel, SettingsSectionViewModelDesign>()
                .AddScoped<IPenaltiesViewModel, PenaltiesViewModel>()
                .AddScoped<IRecoverViewModel, RecoverViewModel>()
                .AddScoped<IManageFundsViewModel, ManageFundsViewModelDesign>()
                .AddScoped<Func<ProjectDto, IFounderProjectViewModel>>(provider => dto => ActivatorUtilities.CreateInstance<FounderProjectViewModel>(provider, dto))
                .AddScoped<Func<ProjectDto, IFounderProjectDetailsViewModel>>(provider => dto => ActivatorUtilities.CreateInstance<FounderProjectDetailsViewModel>(provider, dto))
            ;
    }
}