using Angor.Client;
using Angor.Shared;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using Angor.Wallet.Application;
using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Impl;
using Angor.Wallet.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace Angor.Wallet.Infrastructure;

public static class WalletServices
{
    public static ServiceCollection Register(ServiceCollection services, ILogger logger, BitcoinNetwork bitcoinNetwork)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        
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
        services.AddHttpClient();
        services.AddSingleton<ITransactionWatcher, TransactionWatcher>();
        
        services.TryAddSingleton(loggerFactory);

        return services;
    }
}