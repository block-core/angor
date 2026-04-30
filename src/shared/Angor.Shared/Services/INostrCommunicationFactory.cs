using Nostr.Client.Client;

namespace Angor.Shared.Services;

public interface INostrCommunicationFactory
{
    INostrClient GetOrCreateClient(INetworkService networkService);
    INostrClient GetOrCreateDiscoveryClients(INetworkService networkService);
    void CloseClientConnection();
    int GetNumberOfRelaysConnected();
    bool EoseEventReceivedOnAllRelays(string subscription);
    bool MonitoringEoseReceivedOnSubscription(string subscription);
    void ClearEoseReceivedOnSubscriptionMonitoring(string subscription);
    bool OkEventReceivedOnAllRelays(string eventId);
    void MonitoringOkReceivedOnSubscription(string eventId);
    void ClearOkReceivedOnSubscriptionMonitoring(string eventId);

    /// <summary>
    /// Raised when a relay disconnects. Subscribers (e.g. RelaySubscriptionsHandling)
    /// should re-evaluate pending EOSE/OK actions that may now be satisfied.
    /// The argument is the disconnected relay name.
    /// </summary>
    event Action<string>? RelayDisconnected;
}