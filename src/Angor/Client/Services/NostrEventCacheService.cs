using System.Text.Json;
using Angor.Client.Storage;
using Angor.Shared.Services;
using Blazored.LocalStorage;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages;

namespace Angor.Client.Services
{
    public class NostrEventCacheService : INostrEventCacheService
    {
        private const string NostrEventCachePrefix = "nostr_event_cache:";
        private const string NostrEventTimestampPrefix = "nostr_event_timestamp:";

        private readonly ISyncLocalStorageService _localStorage;
        private readonly ILogger<NostrEventCacheService> _logger;

        public NostrEventCacheService(ISyncLocalStorageService localStorage, ILogger<NostrEventCacheService> logger)
        {
            _localStorage = localStorage;
            _logger = logger;
        }

        /// <summary>
        /// Gets the latest timestamp for a subscription key
        /// </summary>
        /// <param name="subscriptionKey">The subscription key</param>
        /// <returns>The latest timestamp or null if no events are cached</returns>
        public DateTime? GetLatestTimestamp(string subscriptionKey)
        {
            try
            {
                var key = $"{NostrEventTimestampPrefix}{subscriptionKey}";
                if (_localStorage.ContainKey(key))
                {
                    var timestamp = _localStorage.GetItem<DateTime>(key);
                    _logger.LogInformation("🕒 Using cached timestamp {Timestamp} for subscription {SubscriptionKey}",
                        timestamp, subscriptionKey);
                    return timestamp;
                }
                _logger.LogInformation("🕒 No cached timestamp found for subscription {SubscriptionKey}", subscriptionKey);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest timestamp for subscription {SubscriptionKey}", subscriptionKey);
                return null;
            }
        }

        /// <summary>
        /// Stores a Nostr event in the cache
        /// </summary>
        /// <param name="subscriptionKey">The subscription key</param>
        /// <param name="nostrEvent">The Nostr event to store</param>
        public void StoreEvent(string subscriptionKey, NostrEvent nostrEvent)
        {
            try
            {
                if (nostrEvent?.CreatedAt == null)
                {
                    _logger.LogWarning("Attempted to store null or invalid event for subscription {SubscriptionKey}", subscriptionKey);
                    return;
                }

                // Update the latest timestamp if this event is newer
                var latestTimestamp = GetLatestTimestamp(subscriptionKey);
                if (latestTimestamp == null || nostrEvent.CreatedAt > latestTimestamp)
                {
                    _localStorage.SetItem($"{NostrEventTimestampPrefix}{subscriptionKey}", nostrEvent.CreatedAt.Value);
                    _logger.LogDebug("📝 Updated latest timestamp to {Timestamp} for subscription {SubscriptionKey}",
                        nostrEvent.CreatedAt.Value, subscriptionKey);
                }

                // Store the event in the cache
                var cacheKey = $"{NostrEventCachePrefix}{subscriptionKey}";
                var events = GetCachedEvents(subscriptionKey);

                // Check if the event already exists in the cache
                if (events.Any(e => e.Id == nostrEvent.Id))
                {
                    _logger.LogDebug("🔄 Event {EventId} already exists in cache for subscription {SubscriptionKey}",
                        nostrEvent.Id, subscriptionKey);
                    return;
                }

                events.Add(nostrEvent);
                _logger.LogInformation("💾 Stored new event in cache: ID={Id}, CreatedAt={CreatedAt}, Kind={Kind}, Subject={Subject}",
                    nostrEvent.Id,
                    nostrEvent.CreatedAt,
                    nostrEvent.Kind,
                    nostrEvent.Tags.FindFirstTagValue("subject") ?? "None");

                // Limit the cache size to prevent it from growing too large
                const int maxCacheSize = 100;
                if (events.Count > maxCacheSize)
                {
                    _logger.LogInformation("🧹 Trimming cache for subscription {SubscriptionKey} from {OldCount} to {NewCount} events",
                        subscriptionKey, events.Count, maxCacheSize);
                    events = events.OrderByDescending(e => e.CreatedAt).Take(maxCacheSize).ToList();
                }

                _localStorage.SetItem(cacheKey, events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing event in cache for subscription {SubscriptionKey}", subscriptionKey);
            }
        }

        /// <summary>
        /// Gets all cached events for a subscription key
        /// </summary>
        /// <param name="subscriptionKey">The subscription key</param>
        /// <returns>A list of cached events</returns>
        public List<NostrEvent> GetCachedEvents(string subscriptionKey)
        {
            try
            {
                var cacheKey = $"{NostrEventCachePrefix}{subscriptionKey}";
                if (_localStorage.ContainKey(cacheKey))
                {
                    var events = _localStorage.GetItem<List<NostrEvent>>(cacheKey);
                    _logger.LogInformation("📦 Retrieved {Count} events from local storage for subscription {SubscriptionKey}", events.Count, subscriptionKey);

                    // Log details about each cached event
                    foreach (var evt in events)
                    {
                        _logger.LogDebug("📦 Cached event: ID={Id}, CreatedAt={CreatedAt}, Kind={Kind}, Subject={Subject}",
                            evt.Id,
                            evt.CreatedAt,
                            evt.Kind,
                            evt.Tags.FindFirstTagValue("subject") ?? "None");
                    }

                    return events;
                }
                _logger.LogInformation("📦 No cached events found for subscription {SubscriptionKey}", subscriptionKey);
                return new List<NostrEvent>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached events for subscription {SubscriptionKey}", subscriptionKey);
                return new List<NostrEvent>();
            }
        }

        /// <summary>
        /// Clears all cached events
        /// </summary>
        public void ClearCache()
        {
            try
            {
                int count = 0;
                foreach (var key in _localStorage.Keys())
                {
                    if (key.StartsWith(NostrEventCachePrefix) || key.StartsWith(NostrEventTimestampPrefix))
                    {
                        _localStorage.RemoveItem(key);
                        count++;
                    }
                }
                _logger.LogInformation("🗑️ Cleared {Count} items from Nostr event cache", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing Nostr event cache");
            }
        }
    }
}
