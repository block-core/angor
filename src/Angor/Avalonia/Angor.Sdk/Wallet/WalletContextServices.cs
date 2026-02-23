using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.History;
using Angor.Sdk.Wallet.Infrastructure.Impl;
using Angor.Sdk.Wallet.Infrastructure.Impl.History;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Angor.Sdk.Wallet;

public static class WalletContextServices
{
    public static ServiceCollection Register(ServiceCollection services, ILogger logger, BitcoinNetwork bitcoinNetwork)
    {
        RegisterLogger(services, logger);
        services.AddKeyedSingleton<IStore,InMemoryStore>("memory");
        services.AddSingleton<IWalletAppService, WalletAppService>();
        services.AddSingleton<IHdOperations, HdOperations>();
        var networkConfiguration = new NetworkConfiguration();
        var blockcoreNetwork = bitcoinNetwork == BitcoinNetwork.Mainnet ? new BitcoinMain() : new Angornet();
        networkConfiguration.SetNetwork(blockcoreNetwork);
        services.AddSingleton<INetworkConfiguration>(networkConfiguration);
        services.AddSingleton<INetworkService, NetworkService>();
        services.AddSingleton<INetworkStorage, NetworkStorage>();
        //TODO change the call to use the factory
        services.TryAddScoped<HttpClient>(x => x.GetRequiredService<IHttpClientFactory>().CreateClient());
        services.TryAddSingleton<IIndexerService,MempoolSpaceIndexerApi>();
        services.AddSingleton<IWalletFactory, WalletFactory>();
        services.AddSingleton<IWalletOperations, WalletOperations>();
        services.AddSingleton<IPsbtOperations, PsbtOperations>();
        services.AddSingleton<SensitiveWalletDataProvider>();
        services.TryAddSingleton<ISensitiveWalletDataProvider>(provider => ActivatorUtilities.CreateInstance<FrictionlessSensitiveDataProvider>(provider, provider.GetRequiredService<SensitiveWalletDataProvider>()));
        services.AddSingleton<IWalletStore, WalletStore>();
        services.AddSingleton<ITransactionHistory, TransactionHistory>();
        services.TryAddSingleton<IWalletAccountBalanceService, WalletAccountBalanceService>();
        services.AddHttpClient();

        return services;
    }
    
    private static void RegisterLogger(ServiceCollection services, ILogger logger)
    {
        services.TryAddSingleton<ILoggerFactory>(sp => LoggerFactory.Create(builder => builder.AddSerilog(logger)));
        services.TryAddSingleton(logger);
    }
}
