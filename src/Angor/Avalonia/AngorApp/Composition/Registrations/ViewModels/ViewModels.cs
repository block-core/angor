using System;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;
using AngorApp.Core;
using AngorApp.Core.Factories;
using AngorApp.Model.Projects;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Browse.Details;
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
using Zafiro.UI.Navigation;
using IWalletSectionViewModel = AngorApp.Sections.Wallet.Main.IWalletSectionViewModel;
using WalletSectionViewModel = AngorApp.Sections.Wallet.Main.WalletSectionViewModel;

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
