using Angor.Client;
using Angor.Client.Services;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Shared;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Angor.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Serilog;
using EncryptionService = Angor.Contexts.Funding.Projects.Infrastructure.Impl.EncryptionService;

namespace Angor.Contexts.Funding;

public static class FundingContextServices
{
    public static ServiceCollection Register(ServiceCollection services, ILogger logger)
    {
        var networkConfiguration = new NetworkConfiguration();
        networkConfiguration.SetNetwork(new Angornet());

        services.AddSingleton<IProjectAppService, ProjectAppService>();
        services.AddSingleton<IInvestmentAppService, InvestmentAppService>();
        services.AddSingleton<IInvestmentRepository, InvestmentRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<INostrDecrypter, NostrDecrypter>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateInvestment.CreateInvestmentTransactionHandler).Assembly));
        services.TryAddSingleton<ISerializer, Serializer>();
        services.TryAddSingleton<IRelaySubscriptionsHandling, RelaySubscriptionsHandling>();
        services.TryAddSingleton<IRelayService, RelayService>();
        services.TryAddSingleton<INetworkStorage, NetworkStorage>();
        //TODO change the call to use the factory
        services.TryAddScoped<HttpClient>(x => x.GetRequiredService<IHttpClientFactory>().CreateClient());
        services.TryAddSingleton<IIndexerService,MempoolSpaceIndexerApi>();
        services.TryAddSingleton<INetworkConfiguration>(networkConfiguration);
        services.TryAddSingleton<INetworkService, NetworkService>();
        services.TryAddSingleton<IEncryptionService, EncryptionService>();
        services.TryAddSingleton<INostrCommunicationFactory, NostrCommunicationFactory>();
        services.TryAddSingleton<IInvestorTransactionActions, InvestorTransactionActions>();
        services.TryAddSingleton<IFounderTransactionActions, FounderTransactionActions>();
        services.TryAddSingleton<IInvestmentScriptBuilder, InvestmentScriptBuilder>();
        services.TryAddSingleton<ISeederScriptTreeBuilder, SeederScriptTreeBuilder>();
        services.TryAddSingleton<IProjectScriptsBuilder, ProjectScriptsBuilder>();
        services.TryAddSingleton<IDerivationOperations, DerivationOperations>();
        services.TryAddSingleton<IHdOperations, HdOperations>();
        services.TryAddSingleton<ISignService, SignService>();
        services.TryAddSingleton<ISpendingTransactionBuilder, SpendingTransactionBuilder>();
        services.TryAddSingleton<IInvestmentTransactionBuilder, InvestmentTransactionBuilder>();
        services.TryAddSingleton<ITaprootScriptBuilder, TaprootScriptBuilder>();
        services.TryAddSingleton<IWalletOperations, WalletOperations>();
        services.AddSingleton(GetNostrQueryClient);
        services.AddHttpClient();

        return services;
    }

    private static NostrQueryClient GetNostrQueryClient(IServiceProvider provider)
    {
        var nc = provider.GetRequiredService<INetworkConfiguration>();
        var relayUrls = nc.GetDefaultRelayUrls();
            
        List<INostrClient> nostrClients = new();
        foreach (var url in relayUrls.Select(x => x.Url))
        {
            var nostrWebsocketCommunicator = new NostrWebsocketCommunicator(new Uri(url));
            nostrWebsocketCommunicator.Start().Wait();
            var client = ActivatorUtilities.CreateInstance<NostrWebsocketClient>(provider, nostrWebsocketCommunicator);
            nostrClients.Add(client);
        }
            
        return new NostrQueryClient(nostrClients.ToArray());
    }
}