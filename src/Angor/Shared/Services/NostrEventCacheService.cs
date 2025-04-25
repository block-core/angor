using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages;

namespace Angor.Shared.Services
{
    public interface INostrEventCacheService
    {
        DateTime? GetLatestTimestamp(string subscriptionKey);
        void StoreEvent(string subscriptionKey, NostrEvent nostrEvent);
        List<NostrEvent> GetCachedEvents(string subscriptionKey);
        void ClearCache();
    }
}
