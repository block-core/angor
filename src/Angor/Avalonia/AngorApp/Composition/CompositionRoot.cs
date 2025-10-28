using System.IO;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding;
using Angor.Contexts.Integration.WalletFunding;
using Angor.Contexts.Wallet;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Data.Documents.LiteDb.Extensions;
using Angor.Shared;
using Angor.Shared.Services;
using Angor.Shared.Utilities;
using AngorApp.Composition.Registrations.Sections;
using AngorApp.Composition.Registrations.Services;
using AngorApp.Composition.Registrations.ViewModels;
using AngorApp.Sections.Shell;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Zafiro.UI.Navigation;

namespace AngorApp.Composition;

public static class CompositionRoot
{
    public static IMainViewModel CreateMainViewModel(Control topLevelView, string profileName)
    {
        var services = new ServiceCollection();
        
        var logsDirectory = ApplicationStoragePaths
            .GetLogsDirectory("Angor")
            .OnFailureCompensate(_ => Result.Try(() =>
            {
                var fallback = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(fallback);
                return fallback;
            }))
            .Value;

        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "angor-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 15,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var store = new FileStore("Angor", profileName);
        var networkStorage = new NetworkStorage(store);
        var network = networkStorage.GetNetwork() switch
        {
            "Mainnet" => BitcoinNetwork.Mainnet,
            _ => BitcoinNetwork.Testnet
        };

        services.AddLiteDbDocumentStorage(profileName);
        services.AddKeyedSingleton<IStore>("file", store);
        services.AddSingleton<IStore>(provider => provider.GetKeyedService<IStore>("file")!);
        RegisterLogger(services, logger);

        services.AddSingleton<Func<BitcoinNetwork>>(sp => () =>
        {
            var cfg = sp.GetRequiredService<INetworkConfiguration>();
            var name = cfg.GetNetwork().Name;
            return name == "Main" || name == "Mainnet" ? BitcoinNetwork.Mainnet : BitcoinNetwork.Testnet;
        });

        services
            .AddModelServices()
            .AddViewModels()
            .AddUiServices(topLevelView, profileName);
        
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
