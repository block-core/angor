using Nostr.Client.Messages;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Contexts.Data.Services;

public interface INostrClientWrapper
{
    Task ConnectAsync();
    void Disconnect();
    IObservable<NostrEventResponse> SubscribeToEvents(string subscriptionId, NostrFilter filter);
    void CloseSubscription(string subscriptionId);
    void AddRelay(string relayUrl);
    void RemoveRelay(string relayUrl);
    List<string> GetConfiguredRelays();
    bool IsConnected { get; }
    int ConnectedRelaysCount { get; }
}