using AngorApp.Design;
using AngorApp.Features.Invest;
using AngorApp.Sections.Wallet.CreateAndRecover;
using AngorApp.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition.Registrations;

public static class ModelServices
{
    public static IServiceCollection Register(this IServiceCollection services)
    {
        return services
            .AddSingleton<IWalletProvider, WalletProviderDesign>()
            .AddSingleton<IWalletBuilder, WalletProvider>()
            .AddSingleton<InvestWizard>()
            .AddSingleton<WalletCreationWizard>()
            .AddSingleton<WalletRecoveryWizard>();
    }
}