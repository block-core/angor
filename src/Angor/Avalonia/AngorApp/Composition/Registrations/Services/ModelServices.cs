using Angor.UI.Model.Implementation;
using AngorApp.Design;
using AngorApp.Features.Invest;
using AngorApp.Sections.Wallet.CreateAndImport;
using AngorApp.UI.Controls.Feerate;
using AngorApp.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition.Registrations.Services;

public static class ModelServices
{
    // Model-level services used by the UI layer
    public static IServiceCollection AddModelServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IAmountFactory, AmountFactory>()
            .AddSingleton<IWalletBuilder, WalletProvider>()
            .AddSingleton<InvestWizard>()
            .AddSingleton<WalletCreationWizard>()
            .AddSingleton<IFeeCalculator, FeeCalculatorDesignTime>()
            .AddSingleton<WalletImportWizard>();
    }
}
