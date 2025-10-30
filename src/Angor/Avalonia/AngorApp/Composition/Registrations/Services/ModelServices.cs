using AngorApp.Design;
using AngorApp.Flows.CreateProyect;
using AngorApp.Flows.Invest;
using AngorApp.Flows.SendWalletMoney;
using AngorApp.Sections.Wallet.CreateAndImport;
using AngorApp.UI.Controls.Feerate;
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
            .AddSingleton<WalletImportWizard>();
    }
}
