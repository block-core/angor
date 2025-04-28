using Angor.Client;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.History;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Impl.History;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Angor.Contexts.Wallet;

public static class WalletContextServices
{
    public static ServiceCollection Register(ServiceCollection services, ILogger logger, BitcoinNetwork bitcoinNetwork)
    {
        RegisterLogger(services, logger);
        services.TryAddSingleton<IStore>(new InMemoryStore());
        services.AddSingleton<IWalletAppService, WalletAppService>();
        services.AddSingleton<IHdOperations, HdOperations>();
        var networkConfiguration = new NetworkConfiguration();
        // TODO: set correct network
        networkConfiguration.SetNetwork(new Angornet());
        services.AddSingleton<INetworkConfiguration>(networkConfiguration);
        services.AddSingleton<INetworkService, NetworkService>();
        services.AddSingleton<INetworkStorage, NetworkStorage>();
        services.TryAddSingleton<IIndexerService>(provider => new IndexerService(provider.GetRequiredService<INetworkConfiguration>(), provider.GetRequiredService<IHttpClientFactory>().CreateClient(), provider.GetRequiredService<INetworkService>()));
        services.AddSingleton<IWalletFactory, WalletFactory>();
        services.AddSingleton<IWalletOperations, WalletOperations>();
        services.AddSingleton<ISensitiveWalletDataProvider, SensitiveWalletDataProvider>();
        services.AddSingleton<IWalletStore, WalletStore>();
        services.AddSingleton<ITransactionHistory, TransactionHistory>();
        services.AddHttpClient();
        services.AddSingleton<ITransactionWatcher, TransactionWatcher>();
        
        return services;
    }
    
    private static void RegisterLogger(ServiceCollection services, ILogger logger)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        services.TryAddSingleton(loggerFactory);
        services.AddLogging(builder => builder.AddSerilog());
        services.TryAddSingleton(logger);
    }
}