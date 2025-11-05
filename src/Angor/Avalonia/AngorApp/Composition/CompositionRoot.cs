using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding;
using Angor.Contexts.Integration.WalletFunding;
using Angor.Contexts.Wallet;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Data.Documents.LiteDb.Extensions;
using Angor.Shared;
using Angor.Shared.Services;
using AngorApp.Core;
using AngorApp.Composition.Registrations.Sections;
using AngorApp.Composition.Registrations.Services;
using AngorApp.Composition.Registrations.ViewModels;
using AngorApp.Sections.Shell;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zafiro.UI.Navigation;

namespace AngorApp.Composition;

public static class CompositionRoot
{
    public static IMainViewModel CreateMainViewModel(Control topLevelView, string profileName)
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
            .AddUiServices(topLevelView, profileContext, applicationStorage);
        
        services.AddNavigator(logger);
        services.AddSecurityContext();
        RegisterWalletServices(services, logger, network);
        FundingContextServices.Register(services, logger);

        // Integration services
        services.AddSingleton<ISeedwordsProvider, SeedwordsProvider>();

        services.AddAppSections(logger);

        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<INetworkService>().AddSettingsIfNotExist();

        return serviceProvider.GetRequiredService<IMainViewModel>();
    }


    private static void RegisterWalletServices(ServiceCollection services, ILogger logger, BitcoinNetwork network)
    {
        WalletContextServices.Register(services, logger, network);
    }
}
