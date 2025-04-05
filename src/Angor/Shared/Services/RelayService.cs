using System.Reactive.Linq;
using Angor.Shared.Models;
using Nostr.Client.Requests;
using Microsoft.Extensions.Logging;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Metadata;
using Nostr.Client.Responses;

namespace Angor.Shared.Services
{
    public class RelayService : IRelayService
    {
        private readonly ILogger<RelayService> _logger;
        private readonly INostrCommunicationFactory _communicationFactory;
        private readonly INetworkService _networkService;
        private readonly IRelaySubscriptionsHandling _subscriptionsHandling;
        private readonly ISerializer _serializer;
        private readonly INostrEventCacheService _nostrEventCacheService;


        public RelayService(
            ILogger<RelayService> logger,
            INostrCommunicationFactory communicationFactory,
            INetworkService networkService,
            IRelaySubscriptionsHandling subscriptionsHanding,
            ISerializer serializer,
            INostrEventCacheService nostrEventCacheService)
        {
            _logger = logger;
            _communicationFactory = communicationFactory;
            _networkService = networkService;
            _subscriptionsHandling = subscriptionsHanding;
            _serializer = serializer;
            _nostrEventCacheService = nostrEventCacheService;
        }

        public void LookupProjectsInfoByEventIds<T>(Action<T> responseDataAction, Action? OnEndOfStreamAction,
            params string[] nostrEventIds)
        {
            const string subscriptionName = "ProjectInfoLookups";

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            if (nostrClient == null)
                throw new InvalidOperationException("The nostr client is null");

            if (!_subscriptionsHandling.RelaySubscriptionAdded(subscriptionName))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionName)
                    .Select(_ => _.Event)
                    .Subscribe(ev => { responseDataAction(_serializer.Deserialize<T>(ev.Content)); });

                _subscriptionsHandling.TryAddRelaySubscription(subscriptionName, subscription);
            }

            if (OnEndOfStreamAction != null)
            {
                _subscriptionsHandling.TryAddEoseAction(subscriptionName, OnEndOfStreamAction);
            }

            var request = new NostrRequest(subscriptionName, new NostrFilter
            {
                Ids = nostrEventIds,
                Kinds = [NostrKind.ApplicationSpecificData]
            });

            nostrClient.Send(request);
        }

        public void RequestProjectCreateEventsByPubKey(Action<NostrEvent> onResponseAction, Action? onEoseAction,params string[] nPubs)
        {
            var subscriptionKey = Guid.NewGuid().ToString().Replace("-","");

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            if (!_subscriptionsHandling.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event is not null)
                    .Select(_ => _.Event)
                    .Subscribe(onResponseAction!);

                _subscriptionsHandling.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            if (onEoseAction != null)
            {
                _subscriptionsHandling.TryAddEoseAction(subscriptionKey, onEoseAction);
            }

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = nPubs,
                Kinds = [NostrKind.ApplicationSpecificData, NostrKind.Metadata],
            }));
        }

        public Task LookupSignaturesDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit, Action<NostrEvent> onResponseAction)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            var subscriptionKey = nostrPubKey + "DM";

            // Process cached events first
            var cachedEvents = _nostrEventCacheService.GetCachedEvents(subscriptionKey);
            _logger.LogDebug($"Found {cachedEvents.Count} cached events for subscription {subscriptionKey}");

            foreach (var cachedEvent in cachedEvents
                .Where(e => e.Kind == NostrKind.EncryptedDm))
            {
                onResponseAction(cachedEvent);
            }

            if (!_subscriptionsHandling.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event is not null)
                    .Select(_ => _.Event)
                    .Subscribe(@event =>
                    {
                        // Store the event in the cache
                        _nostrEventCacheService.StoreEvent(subscriptionKey, @event);
                        onResponseAction(@event);
                    });

                _subscriptionsHandling.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            // Get the latest timestamp from the cache if no specific since parameter is provided
            var latestTimestamp = since ?? _nostrEventCacheService.GetLatestTimestamp(subscriptionKey);

            // Log the request to the relay
            if (latestTimestamp.HasValue)
            {
                _logger.LogInformation("📬 Fetching new signature DMs from relay since {Timestamp} for subscription {SubscriptionKey}",
                    latestTimestamp.Value, subscriptionKey);
            }
            else
            {
                _logger.LogInformation("📬 Fetching all signature DMs from relay (no timestamp) for subscription {SubscriptionKey}",
                    subscriptionKey);
            }

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                P = new[] { nostrPubKey },
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = latestTimestamp,
                Limit = limit
            }));

            return Task.CompletedTask;
        }

        public Task LookupDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit, Func<NostrEvent,Task> onResponseAction, string? senderNpub = null)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            var subscriptionKey = nostrPubKey;

            // Process cached events first
            var cachedEvents = _nostrEventCacheService.GetCachedEvents(subscriptionKey);
            _logger.LogDebug($"Found {cachedEvents.Count} cached events for subscription {subscriptionKey}");

            foreach (var cachedEvent in cachedEvents
                .Where(e => e.Kind == NostrKind.EncryptedDm)
                .Where(e => senderNpub == null || e.Pubkey == senderNpub))
            {
                onResponseAction(cachedEvent);
            }

            if (!_subscriptionsHandling.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event is not null)
                    .Select(_ => _.Event)
                    .Subscribe(@event =>
                    {
                        // Store the event in the cache
                        _nostrEventCacheService.StoreEvent(subscriptionKey, @event);
                        onResponseAction(@event);
                    });

                _subscriptionsHandling.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            // Get the latest timestamp from the cache if no specific since parameter is provided
            var latestTimestamp = since ?? _nostrEventCacheService.GetLatestTimestamp(subscriptionKey);

            // Log the request to the relay
            if (latestTimestamp.HasValue)
            {
                _logger.LogInformation("📬 Fetching new direct messages from relay since {Timestamp} for subscription {SubscriptionKey}",
                    latestTimestamp.Value, subscriptionKey);
            }
            else
            {
                _logger.LogInformation("📬 Fetching all direct messages from relay (no timestamp) for subscription {SubscriptionKey}",
                    subscriptionKey);
            }

            var nostrFilter = new NostrFilter
            {
                P = new[] { nostrPubKey },
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = latestTimestamp,
                Limit = limit
            };

            if (senderNpub != null)
            {
                nostrFilter.Authors = new[] { senderNpub };
                _logger.LogDebug("📬 Filtering relay request by sender: {SenderNpub}", senderNpub);
            }

            nostrClient.Send(new NostrRequest(subscriptionKey, nostrFilter));

            return Task.CompletedTask;
        }

        public string SendDirectMessagesForPubKeyAsync(string senderNosterPrivateKey, string nostrPubKey, string encryptedMessage, Action<NostrOkResponse> onResponseAction)
        {
            var sender = NostrPrivateKey.FromHex(senderNosterPrivateKey);

            var client = _communicationFactory.GetOrCreateClient(_networkService);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedMessage,
                Tags = new NostrEventTags(NostrEventTag.Profile(nostrPubKey))
            };

            var signed = ev.Sign(sender);

            // Store the event in the cache
            var subscriptionKey = nostrPubKey;
            _nostrEventCacheService.StoreEvent(subscriptionKey, signed);

            _logger.LogInformation("📩 Sending direct message to {RecipientPubKey}, event ID: {EventId}",
                nostrPubKey, signed.Id);

            if (!_subscriptionsHandling.TryAddOKAction(signed.Id!,onResponseAction))
                throw new InvalidOperationException(
                    $"Failed to add ok action to monitoring of relay results {signed.Id}");

            client.Send(new NostrEventRequest(signed));

            return signed.Id!;
        }

        public void CloseConnection()
        {
            _subscriptionsHandling.Dispose();
        }

        public void LookupNostrProfileForNPub(Action<string,ProjectMetadata> onResponse, Action onEndOfStream, params string[] npubs)
        {
            var client = _communicationFactory.GetOrCreateClient(_networkService);

            var subscriptionKey = Guid.NewGuid().ToString().Replace("-","");

            if (!_subscriptionsHandling.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = client.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event is not null)
                    .Select(_ => _.Event as NostrMetadataEvent)
                    .Subscribe(@event => onResponse(@event.Pubkey, ProjectMetadata.Parse(_serializer.Deserialize<NostrMetadata>(@event.Content))));

                _subscriptionsHandling.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            if (onEndOfStream != null)
            {
                _subscriptionsHandling.TryAddEoseAction(subscriptionKey, onEndOfStream);
            }

            client.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = npubs,
                Kinds = [NostrKind.Metadata],
            }));
        }

        public Task<string> AddProjectAsync(ProjectInfo project, string hexPrivateKey, Action<NostrOkResponse> action)
        {
            var key = NostrPrivateKey.FromHex(hexPrivateKey);

            if (!project.NostrPubKey.Contains(key.DerivePublicKey().Hex))
                throw new ArgumentException($"The nostr pub key on the project does not fit the npub calculated from the nsec {project.NostrPubKey} {key.DerivePublicKey().Hex}");

            var content = _serializer.Serialize(project);

            var signed = GetNip78NostrEvent(content)
                .Sign(key);

            _subscriptionsHandling.TryAddOKAction(signed.Id,action);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            nostrClient.Send(new NostrEventRequest(signed));

            return Task.FromResult(signed.Id);
        }

        public Task<string> CreateNostrProfileAsync(NostrMetadata metadata, string hexPrivateKey, Action<NostrOkResponse> action)
        {
            var key = NostrPrivateKey.FromHex(hexPrivateKey);

            var content = _serializer.Serialize(metadata);

            var signed = new NostrEvent
                {
                    Kind = NostrKind.Metadata,
                    CreatedAt = DateTime.UtcNow,
                    Content = content
                }.Sign(key);

            _subscriptionsHandling.TryAddOKAction(signed.Id,action);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            nostrClient.Send(new NostrEventRequest(signed));

            return Task.FromResult(signed.Id);
        }

        public Task<string> DeleteProjectAsync(string eventId, string hexPrivateKey)
        {
            var key = NostrPrivateKey.FromHex(hexPrivateKey);

            var deleteEvent = new NostrEvent
            {
                Kind = NostrKind.EventDeletion,
                CreatedAt = DateTime.UtcNow,
                Content = "Failed to publish the transaction to the blockchain",
                Tags = new NostrEventTags(NostrEventTag.Event(eventId))
            }.Sign(key);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(deleteEvent);

            return Task.FromResult(deleteEvent.Id);
        }

        private static NostrEvent GetNip78NostrEvent( string content)
        {
            var ev = new NostrEvent
            {
                Kind = NostrKind.ApplicationSpecificData,
                CreatedAt = DateTime.UtcNow,
                Content = content
            };
            return ev;
        }

        private static NostrEvent GetNip99NostrEvent(ProjectInfo project, string content)
        {
            var ev = new NostrEvent
            {
                Kind = (NostrKind)30402,
                CreatedAt = DateTime.UtcNow,
                Content = content,
                Tags = new NostrEventTags( //TODO need to find the correct tags for the event
                    new NostrEventTag("d", "AngorApp", "Create a new project event"),
                    new NostrEventTag("title", "New project :)"),
                    new NostrEventTag("published_at", DateTime.UtcNow.ToString()),
                    new NostrEventTag("t","#AngorProjectInfo"),
                    new NostrEventTag("image",""),
                    new NostrEventTag("summary","A new project that will save the world"),
                    new NostrEventTag("location",""),
                    new NostrEventTag("price","1","BTC"))
            };

            return ev;
        }
    }

}
