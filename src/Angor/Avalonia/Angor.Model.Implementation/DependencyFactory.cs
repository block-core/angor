using Angor.Shared.Services;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;

namespace Angor.Model.Implementation;

public static class DependencyFactory
{
    public static IRelayService GetRelayService(ILoggerFactory loggerFactory)
    {
        var relaySubscriptionsLogger = loggerFactory.CreateLogger<RelaySubscriptionsHandling>();
        var relayServiceLogger = loggerFactory.CreateLogger<RelayService>();
        var clientLogger = loggerFactory.CreateLogger<NostrWebsocketClient>();
        var networkServiceLogger = loggerFactory.CreateLogger<NetworkService>();
        var nostrCommunicationFactoryLogger = loggerFactory.CreateLogger<NostrCommunicationFactory>();

        var networkConfiguration = new NetworkConfiguration();
        var httpClient = new HttpClient();
        var networkService = new NetworkService(new InMemoryStorage(), httpClient, networkServiceLogger, networkConfiguration);
        var nostrCommunicationFactory = new NostrCommunicationFactory(clientLogger, nostrCommunicationFactoryLogger);
        var relaySubscriptionsHandling = new RelaySubscriptionsHandling(relaySubscriptionsLogger, nostrCommunicationFactory, networkService);
        
        var relay = new RelayService(relayServiceLogger, nostrCommunicationFactory, networkService, relaySubscriptionsHandling, new Serializer());
        return relay;
    }

    public static IIndexerService GetIndexerService(ILoggerFactory loggerFactory)
    {
        var networkServiceLogger = loggerFactory.CreateLogger<NetworkService>();
        var networkConfiguration = new NetworkConfiguration();
        var networkConfig = networkConfiguration;
        var httpClient = new HttpClient();
        var networkService = new NetworkService(new InMemoryStorage(), httpClient, networkServiceLogger, networkConfiguration);
        
        return new IndexerService(networkConfig, httpClient, networkService);
    }
}