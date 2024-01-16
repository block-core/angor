using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public interface IRelaySubscriptionsHandling
{
    void HandleOkMessages(NostrOkResponse _);
    bool TryAddEoseAction(string subscriptionName, Action action);
    void HandleEoseMessages(NostrEoseResponse _);
    bool TryAddRelaySubscription(string subscriptionKey, IDisposable subscription);
    void Dispose();
}