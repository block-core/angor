using AngorApp.Design;
using AngorApp.UI.Flows.CreateProject;
using AngorApp.UI.Flows.Invest;
using AngorApp.UI.Flows.SendWalletMoney;
using AngorApp.UI.Sections.Wallet.CreateAndImport;
using AngorApp.UI.Shared.Controls.Feerate;
using Branta.V2.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition.Registrations.Services;

public static class ModelServices
{
    // Model-level services used by the UI layer
    public static IServiceCollection AddModelServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IAmountFactory, AmountFactory>()
            .AddSingleton<IWalletProvider, SimpleWalletProvider>()
            .AddScoped<ICreateProjectFlow, CreateProjectFlow>()
            .AddScoped<ISendMoneyFlow, SendMoneyFlow>()
            .AddSingleton<InvestFlow>()
            .AddSingleton<WalletCreationWizard>()
            .AddSingleton<IFeeCalculator, FeeCalculatorDesignTime>()
            .AddSingleton<WalletImportWizard>()
            .ConfigureBrantaServices();
    }
}
