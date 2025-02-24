using Angor.UI.Model.Implementation.Projects;
using AngorApp.Design;
using AngorApp.Sections.Wallet.CreateAndRecover;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition.Registrations;

public static class ModelServices
{
    public static IServiceCollection Register(this IServiceCollection services)
    {
        return services
            .AddSingleton<IWalletProvider, WalletProviderDesign>()
            .AddSingleton<IWalletBuilder, WalletBuilderDesign>()
            .AddSingleton<IWalletWizard, WalletWizard>()
            .AddSingleton<IProjectService, ProjectService>();
    }
}