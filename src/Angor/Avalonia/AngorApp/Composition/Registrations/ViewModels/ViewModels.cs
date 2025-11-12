using Angor.Contexts.Funding.Projects.Application.Dtos;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;
using AngorApp.Core.Factories;
using AngorApp.UI.Sections.Browse;
using AngorApp.UI.Sections.Browse.Details;
using AngorApp.UI.Sections.Browse.ProjectLookup;
using AngorApp.UI.Sections.Founder;
using AngorApp.UI.Sections.Founder.ProjectDetails;
using AngorApp.UI.Sections.Home;
using AngorApp.UI.Sections.Portfolio;
using AngorApp.UI.Sections.Portfolio.Penalties;
using AngorApp.UI.Sections.Portfolio.Recover;
using AngorApp.UI.Sections.Settings;
using AngorApp.UI.Sections.Shell;
using AngorApp.UI.Shared.Controls.Common.FoundedProjectOptions;
using Microsoft.Extensions.DependencyInjection;
using IWalletSectionViewModel = AngorApp.UI.Sections.Wallet.Main.IWalletSectionViewModel;
using WalletSectionViewModel = AngorApp.UI.Sections.Wallet.Main.WalletSectionViewModel;

namespace AngorApp.Composition.Registrations.ViewModels;

public static class ViewModels
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        return services.AddScoped<IMainViewModel, MainViewModel>()
                .AddScoped<IProjectViewModelFactory, ProjectViewModelFactory>()
                .AddScoped<IProjectInvestCommandFactory, ProjectInvestCommandFactory>()
                .AddScoped<Func<IProjectLookupViewModel>>(provider => () => ActivatorUtilities.CreateInstance<ProjectLookupViewModel>(provider))
                .AddScoped<Func<ProjectId, IFoundedProjectOptionsViewModel>>(provider => projectId => ActivatorUtilities.CreateInstance<FoundedProjectOptionsViewModel>(provider, projectId))
                .AddScoped<Func<FullProject, IProjectDetailsViewModel>>(provider => project => ActivatorUtilities.CreateInstance<ProjectDetailsViewModel>(provider, project))
                .AddScoped<Func<ProjectId, IFounderProjectDetailsViewModel>>(provider => projectId => ActivatorUtilities.CreateInstance<FounderProjectDetailsViewModel>(provider, projectId))
                .AddScoped<Func<ProjectDto, IFounderProjectViewModel>>(provider => dto => ActivatorUtilities.CreateInstance<FounderProjectViewModel>(provider, dto))
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
