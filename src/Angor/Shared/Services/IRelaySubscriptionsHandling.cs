using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public interface IRelaySubscriptionsHandling
{
    bool TryAddOKAction(string eventId, Action<NostrOkResponse> action);
    void HandleOkMessages(NostrOkResponse _);
    bool TryAddEoseAction(string subscriptionName, Action action);
    void HandleEoseMessages(NostrEoseResponse _);
    bool RelaySubscriptionAdded(string subscriptionKey);
    bool TryAddRelaySubscription(string subscriptionKey, IDisposable subscription);
    void Dispose();
}