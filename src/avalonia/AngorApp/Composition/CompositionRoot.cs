using Angor.Sdk.Common;
using Angor.Sdk.Funding;
using Angor.Sdk.Integration;
using Angor.Sdk.Wallet;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Impl;
using Angor.Data.Documents.LiteDb.Extensions;
using Angor.Shared;
using Angor.Shared.Services;
using AngorApp.Core;
using AngorApp.Composition.Registrations.Services;
using AngorApp.Composition.Registrations.ViewModels;
using AngorApp.Model.Contracts.Amounts;
using AngorApp.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AngorApp.Composition;

public static class CompositionRoot
{
    public static IShellViewModel CreateMainViewModel(Control topLevelView, string profileName)
    {
        var services = new ServiceCollection();
        var applicationStorage = new ApplicationStorage();
        var profileContext = new ProfileContext("Angor", applicationStorage.SanitizeProfileName(profileName));
        var logger = LoggingConfigurator.CreateLogger(profileContext.AppName, applicationStorage);
        UnhandledExceptionLogger.Register(logger);

        var store = new FileStore(applicationStorage, profileContext);
        var networkStorage = new NetworkStorage(store);
        var network = networkStorage.GetNetwork() switch
        {
            "Mainnet" => BitcoinNetwork.Mainnet,
            _ => BitcoinNetwork.Testnet
        };

        services.AddSingleton<IApplicationStorage>(applicationStorage);
        services.AddLiteDbDocumentStorage(profileContext);
        services.AddKeyedSingleton<IStore>("file", store);
        services.AddSingleton<IStore>(provider => provider.GetKeyedService<IStore>("file")!);
        LoggingConfigurator.RegisterLogger(services, logger);

        services.AddSingleton<Func<BitcoinNetwork>>(sp => () =>
        {
            var cfg = sp.GetRequiredService<INetworkConfiguration>();
            var name = cfg.GetNetwork().Name;
            return name == "Main" || name == "Mainnet" ? BitcoinNetwork.Mainnet : BitcoinNetwork.Testnet;
        });

        services
            .AddModelServices()
            .AddViewModels()
            .AddUIServices(topLevelView, profileContext, applicationStorage);
        
        services.AddSecurityContext();
        RegisterWalletServices(services, logger, network);
        FundingContextServices.Register(services, logger);

        // Integration services
        services.AddSingleton<ISeedwordsProvider, SeedwordsProvider>();
        services.AddAllSectionsFromAttributes(logger);

        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<INetworkService>().AddSettingsIfNotExist();

        // Ensure AmountFactory is created early so AmountUI.DefaultSymbol is set from network config
        serviceProvider.GetRequiredService<IAmountFactory>();

        return serviceProvider.GetRequiredService<IShellViewModel>();
    }


    private static void RegisterWalletServices(ServiceCollection services, ILogger logger, BitcoinNetwork network)
    {
        WalletContextServices.Register(services, logger, network);
    }
}
