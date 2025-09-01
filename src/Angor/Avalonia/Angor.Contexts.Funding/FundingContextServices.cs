using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Angor.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

namespace Angor.Contexts.Funding;

public static class FundingContextServices
{
    public static ServiceCollection Register(ServiceCollection services, ILogger logger)
    {
        var networkConfiguration = new NetworkConfiguration();

        
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
        services.TryAddSingleton<IProjectInvestmentsRepository, ProjectInvestmentsRepository>();
        
        services.AddHttpClient();
        
        services.AddSingleton<IProjectAppService, ProjectAppService>();
        services.AddSingleton<IInvestmentAppService, InvestmentAppService>();
        
        return services;
    }
}