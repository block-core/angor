using Nostr.Client.Client;

namespace Angor.Shared.Services;

public interface INostrCommunicationFactory
{
    INostrClient GetOrCreateClient(INetworkService networkService);
    void CloseClientConnection();
    int GetNumberOfRelaysConnected();
    bool EventReceivedOnAllRelays(string subscription);
    void MonitoringEoseReceivedOnSubscription(string subscription);
    void ClearEoseReceivedOnSubscriptionMonitoring(string subscription);
}