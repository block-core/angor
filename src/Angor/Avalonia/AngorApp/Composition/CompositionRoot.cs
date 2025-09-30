using Angor.Contests.CrossCutting;
using Angor.Contexts.Data;
using Angor.Contexts.Funding;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Integration.WalletFunding;
using Angor.Contexts.Wallet;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Shared;
using Angor.Shared.Services;
using AngorApp.Composition.Registrations.Sections;
using AngorApp.Composition.Registrations.Services;
using AngorApp.Composition.Registrations.ViewModels;
using AngorApp.Sections.Shell;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Zafiro.UI.Navigation;

namespace AngorApp.Composition;

public static class CompositionRoot
{
    public static IMainViewModel CreateMainViewModel(Control topLevelView, IConfigurationRoot configuration)
    {
        var services = new ServiceCollection();
        
        DataContextServices.Register(services);
        
        
        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug().CreateLogger();

        var store = new FileStore("Angor");
        var networkStorage = new NetworkStorage(store);
        var network = networkStorage.GetNetwork() switch
        {
            "Mainnet" => BitcoinNetwork.Mainnet,
            _ => BitcoinNetwork.Testnet
        };

        RegisterLogger(services, logger);
        services.AddKeyedSingleton<IStore>("file", store);
        services.AddSingleton<IStore>(provider => provider.GetKeyedService<IStore>("file"));

        services.AddSingleton<Func<BitcoinNetwork>>(sp => () =>
        {
            var cfg = sp.GetRequiredService<INetworkConfiguration>();
            var name = cfg.GetNetwork().Name;
            return name == "Main" || name == "Mainnet" ? BitcoinNetwork.Mainnet : BitcoinNetwork.Testnet;
        });

        services
            .AddModelServices()
            .AddViewModels()
            .AddUiServices(topLevelView);
        
        services.AddNavigator();
        services.AddSecurityContext();
        RegisterWalletServices(services, logger, network);
        FundingContextServices.Register(services, logger);

        // Integration services
        services.AddSingleton<ISeedwordsProvider, SeedwordsProvider>();

        services.AddAppSections(logger);

        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<INetworkService>().AddSettingsIfNotExist();

        DataContextServices.ApplyMigrations(serviceProvider);
        
        return serviceProvider.GetRequiredService<IMainViewModel>();
    }


    private static void RegisterWalletServices(ServiceCollection services, Logger logger, BitcoinNetwork network)
    {
        WalletContextServices.Register(services, logger, network);
    }

    private static void RegisterLogger(ServiceCollection services, Logger logger)
    {
        services.AddLogging(builder => builder.AddSerilog());
        services.AddSingleton<ILogger>(logger);
    }
}