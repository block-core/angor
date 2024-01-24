using Nostr.Client.Client;

namespace Angor.Shared.Services;

public interface INostrCommunicationFactory
{
    INostrClient GetOrCreateClient(INetworkService networkService);
    void CloseClientConnection();
    int GetNumberOfRelaysConnected();
    bool EoseEventReceivedOnAllRelays(string subscription);
    void MonitoringEoseReceivedOnSubscription(string subscription);
    void ClearEoseReceivedOnSubscriptionMonitoring(string subscription);
    bool OkEventReceivedOnAllRelays(string eventId);
    void MonitoringOkReceivedOnSubscription(string eventId);
    void ClearOkReceivedOnSubscriptionMonitoring(string eventId);
}