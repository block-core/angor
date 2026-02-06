using Angor.Sdk.Common;
using Angor.Sdk.Common.MediatR;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Shared;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Angor.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

namespace Angor.Sdk.Funding;

public static class FundingContextServices
{
    public static ServiceCollection Register(ServiceCollection services, ILogger logger)
    {
        var networkConfiguration = new NetworkConfiguration();

        
        services.AddSingleton<IPortfolioService, PortfolioService>();
        //services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectService, DocumentProjectService>();
        services.AddSingleton<INostrDecrypter, NostrDecrypter>();
        services.AddSingleton<IInvestmentHandshakeService, InvestmentHandshakeService>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(BuildInvestmentDraft.BuildInvestmentDraftHandler).Assembly);
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
        });
        services.TryAddSingleton<ISerializer, Serializer>();
        services.TryAddSingleton<IRelaySubscriptionsHandling, RelaySubscriptionsHandling>();
        services.TryAddSingleton<IRelayService, RelayService>();
        services.TryAddSingleton<INetworkStorage, NetworkStorage>();
        //TODO change the call to use the factory
        services.TryAddScoped<HttpClient>(x => x.GetRequiredService<IHttpClientFactory>().CreateClient());
        services.TryAddSingleton<IIndexerService,MempoolSpaceIndexerApi>();
        services.TryAddSingleton<MempoolIndexerMappers>();
        services.TryAddSingleton<IAngorIndexerService, MempoolIndexerAngorApi>();
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
        services.TryAddSingleton<IProjectInvestmentsService, ProjectInvestmentsService>();
        services.TryAddSingleton<ITransactionService,TransactionService>();
        services.TryAddSingleton<IWalletAccountBalanceService, WalletAccountBalanceService>();
        services.TryAddSingleton<IMempoolMonitoringService, MempoolMonitoringService>();
        
        // Lightning Network / Boltz submarine swap services
        // Using testnet by default for development
        services.TryAddSingleton<BoltzConfiguration>(_ => new BoltzConfiguration
        {
            BaseUrl = Environment.GetEnvironmentVariable("BOLTZ_API_URL") ?? BoltzConfiguration.TestnetUrl,
            TimeoutSeconds = 30
        });
        services.TryAddSingleton<IBoltzSwapService, BoltzSwapService>();
        
        //services.AddHttpClient();
        
        services.AddSingleton<IProjectAppService, ProjectAppService>();
        services.AddSingleton<IInvestmentAppService, InvestmentAppService>();
        services.AddSingleton<IFounderAppService, FounderAppService>();
        
        return services;
    }
}
