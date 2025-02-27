using Angor.Client;
using Angor.Shared;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using Angor.Wallet.Application;
using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Impl;
using Angor.Wallet.Infrastructure.Interfaces;
using Angor.Wallet.Tests.Infrastructure.TestDoubles;
using Microsoft.Extensions.Logging;

namespace Angor.Wallet.Tests.Infrastructure;

public class WalletAppServiceFixture : IAsyncLifetime
{
    public IWalletAppService WalletAppService { get; private set; }
    public WalletId WalletId { get; } = Wallet.Infrastructure.Impl.WalletAppService.SingleWalletId;

    public async Task InitializeAsync()
    {
        // Use Angornet
        var network = new Angornet();
        var networkConfig = new NetworkConfiguration();
        networkConfig.SetNetwork(network);

        // Create dependencies
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var httpClient = new HttpClient();

        var inMemoryStorage = new NetworkStorage();
        var networkService = new NetworkService(
            inMemoryStorage,
            httpClient,
            loggerFactory.CreateLogger<NetworkService>(),
            networkConfig);

        var indexerService = new IndexerService(networkConfig, httpClient, networkService);
        var hdOperations = new HdOperations();

        var walletOperations = new WalletOperations(
            indexerService,
            hdOperations,
            loggerFactory.CreateLogger<WalletOperations>(),
            networkConfig);

        var sensitiveWalletDataProvider = new TestSensitiveWalletDataProvider(
            "print foil moment average quarter keep amateur shell tray roof acoustic where",
            ""
        );

        var store = new WalletStore(new InMemoryStore());
        var walletSecurityContext = new TestSecurityContext();

        IWalletFactory walletFactory = new WalletFactory(store, walletSecurityContext);
        WalletAppService = new WalletAppService(sensitiveWalletDataProvider, indexerService, walletFactory, walletOperations);
    }

    public Task DisposeAsync()
    {
        // Cleanup resources if needed
        return Task.CompletedTask;
    }
}