using Angor.Client;
using Angor.Client.Services;
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Impl;
using Angor.Contexts.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Networks;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Angor.Shared.Services;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using EncryptionService = Angor.Contexts.Projects.Infrastructure.Impl.EncryptionService;

namespace Angor.Contexts.Projects.Infrastructure;

using EncryptionService = Impl.EncryptionService;

public class ProjectServices
{
    public static ServiceCollection Register(ServiceCollection services, ILogger logger)
    {
        var networkConfiguration = new NetworkConfiguration();
        networkConfiguration.SetNetwork(new Angornet());

        services.AddSingleton<IProjectAppService, ProjectAppService>();
        services.AddSingleton<InvestCommandFactory>();
        services.AddSingleton<IInvestmentRepository, InvestmentRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        
        services.TryAddSingleton<ISerializer, Serializer>();
        services.TryAddSingleton<IRelaySubscriptionsHandling, RelaySubscriptionsHandling>();
        services.TryAddSingleton<IRelayService, RelayService>();
        services.TryAddSingleton<INetworkStorage, NetworkStorage>();
        services.TryAddSingleton<IIndexerService>(provider => new IndexerService(provider.GetRequiredService<INetworkConfiguration>(), provider.GetRequiredService<IHttpClientFactory>().CreateClient(), provider.GetRequiredService<INetworkService>()));
        services.TryAddSingleton<INetworkConfiguration>(networkConfiguration);
        services.TryAddSingleton<INetworkService, NetworkService>();
        services.TryAddSingleton<IEncryptionService, EncryptionService>();
        services.TryAddSingleton<INostrCommunicationFactory, NostrCommunicationFactory>();
        services.TryAddSingleton<IInvestorTransactionActions, InvestorTransactionActions>();
        services.TryAddSingleton<IInvestmentScriptBuilder, InvestmentScriptBuilder>();
        services.TryAddSingleton<ISeederScriptTreeBuilder, SeederScriptTreeBuilder>();
        services.TryAddSingleton<IProjectScriptsBuilder, ProjectScriptsBuilder>();
        services.TryAddSingleton<IDerivationOperations, DerivationOperations>();
        services.TryAddSingleton<IHdOperations, HdOperations>();
        services.TryAddSingleton<ISpendingTransactionBuilder, SpendingTransactionBuilder>();
        services.TryAddSingleton<IInvestmentTransactionBuilder, InvestmentTransactionBuilder>();
        services.TryAddSingleton<ITaprootScriptBuilder, TaprootScriptBuilder>();
        services.AddHttpClient();

        return services;
    }
}