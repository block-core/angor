using Angor.Sdk.Funding.Projects.Application.Dtos;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;
using AngorApp.Core.Factories;
using AngorApp.UI.Sections.Browse;
using AngorApp.UI.Sections.Browse.Details;
using AngorApp.UI.Sections.Browse.ProjectLookup;
using AngorApp.UI.Sections.Founder;
using AngorApp.UI.Sections.Founder.ProjectDetails;
using AngorApp.UI.Sections.Portfolio;
using AngorApp.UI.Sections.Portfolio.Penalties;
using AngorApp.UI.Sections.Portfolio.Recover;
using AngorApp.UI.Sections.Settings;
using AngorApp.UI.Shared.Controls.Common.FoundedProjectOptions;
using AngorApp.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using IWalletSectionViewModel = AngorApp.UI.Sections.Wallet.Main.IWalletSectionViewModel;
using ShellViewModel = AngorApp.UI.Shell.ShellViewModel;
using WalletSectionViewModel = AngorApp.UI.Sections.Wallet.Main.WalletSectionViewModel;

namespace AngorApp.Composition.Registrations.ViewModels;

public static class ViewModels
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        return services
                .AddScoped<IProjectViewModelFactory, ProjectViewModelFactory>()
                .AddScoped<IProjectInvestCommandFactory, ProjectInvestCommandFactory>()
                .AddScoped<Func<IProjectLookupViewModel>>(provider => () => ActivatorUtilities.CreateInstance<ProjectLookupViewModel>(provider))
                .AddScoped<Func<ProjectId, IFoundedProjectOptionsViewModel>>(provider => projectId => ActivatorUtilities.CreateInstance<FoundedProjectOptionsViewModel>(provider, projectId))
                .AddScoped<Func<IFullProject, IProjectDetailsViewModel>>(provider => project => ActivatorUtilities.CreateInstance<ProjectDetailsViewModel>(provider, project))
                .AddScoped<Func<ProjectId, IFounderProjectDetailsViewModel>>(provider => projectId => ActivatorUtilities.CreateInstance<FounderProjectDetailsViewModel>(provider, projectId))
                .AddScoped<Func<ProjectDto, IFounderProjectViewModel>>(provider => dto => ActivatorUtilities.CreateInstance<FounderProjectViewModel>(provider, dto))
                .AddTransient<IWalletSectionViewModel, WalletSectionViewModel>()
                .AddTransient<IBrowseSectionViewModel, BrowseSectionViewModel>()
                .AddTransient<IPortfolioSectionViewModel, PortfolioSectionViewModel>()
                .AddTransient<IFounderSectionViewModel, FounderSectionViewModel>()
                .AddTransient<ISettingsSectionViewModel, SettingsSectionViewModel>()
                .AddScoped<IPenaltiesViewModel, PenaltiesViewModel>()
                .AddScoped<IRecoverViewModel, RecoverViewModel>()
                .AddSingleton<IShellViewModel, ShellViewModel>();
    }
}
