using Angor.Client;
using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Impl;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

namespace Angor.Projects;

public class ProjectServices
{
    public static ServiceCollection Register(ServiceCollection services, ILogger logger)
    {
        var networkConfiguration = new NetworkConfiguration();
        networkConfiguration.SetNetwork(new Angornet());

        services.AddSingleton<IProjectAppService, ProjectAppService>();
        services.AddSingleton<IInvestmentService, InvestmentService>();
        services.AddSingleton<IInvestmentRepository, InvestmentRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();

        services.TryAddSingleton<ISerializer, Serializer>();
        services.TryAddSingleton<IRelaySubscriptionsHandling, RelaySubscriptionsHandling>();
        services.TryAddSingleton<IRelayService, RelayService>();
        services.TryAddSingleton<INetworkConfiguration>(networkConfiguration);
        services.TryAddSingleton<INetworkService, NetworkService>();
        services.TryAddSingleton<INostrCommunicationFactory, NostrCommunicationFactory>();

        return services;
    }
}

public class InvestmentService : IInvestmentService
{
    public Task<Result<string>> CreateInvestmentTransaction(string bitcoinAddress, string investorPubKey, long satoshiAmount, ModelFeeRate feeRate)
    {
        throw new NotImplementedException();
    }
}