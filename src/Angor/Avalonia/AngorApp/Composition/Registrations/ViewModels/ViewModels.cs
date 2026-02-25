using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;
using AngorApp.Core.Factories;
using AngorApp.UI.Sections.MyProjects;
using AngorApp.UI.Sections.Settings;
using AngorApp.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using ShellViewModel = AngorApp.UI.Shell.ShellViewModel;
using AngorApp.UI.Flows.InvestV2;

using AngorApp.UI.Flows.AddWallet;
using AngorApp.UI.Flows.AddWallet.SeedBackup;
using AngorApp.UI.Flows.InvestV2.PaymentSelector;
using AngorApp.UI.Sections.Funds.Accounts;
using AngorApp.UI.Sections.Funds.Empty;
using AngorApp.UI.Sections.FindProjects.Details;
using AngorApp.UI.Sections.MyProjects.ManageFunds;
using AngorApp.Model.ProjectsV2;

namespace AngorApp.Composition.Registrations.ViewModels;

public static class ViewModels
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        return services
                .AddScoped<IProjectInvestCommandFactory, ProjectInvestCommandFactory>()
                .AddScoped<Func<IProject, IDetailsViewModel>>(provider => project => ActivatorUtilities.CreateInstance<DetailsViewModel>(provider, project))
                .AddScoped<Func<ProjectId, IManageFundsViewModel>>(provider => project => ActivatorUtilities.CreateInstance<ManageFundsViewModel>(provider, project))
                .AddTransient<IAccountsViewModel, AccountsViewModel>()
                .AddTransient<ISeedBackupFileService, SeedBackupFileService>()
                .AddTransient<IAddWalletFlow, AddWalletFlow>()
                .AddTransient<IEmptyViewModel, EmptyViewModel>()
                .AddTransient<IMyProjectsSectionViewModel, MyProjectsSectionViewModel>()
                .AddTransient<ISettingsSectionViewModel, SettingsSectionViewModel>()
                .AddScoped<Func<IFullProject, IInvestViewModel>>(provider => proj => ActivatorUtilities.CreateInstance<InvestViewModel>(provider, proj))
                .AddScoped<IPaymentSelectorViewModel, PaymentSelectorViewModel>()
                .AddSingleton<IShellViewModel, ShellViewModel>();
    }
}
