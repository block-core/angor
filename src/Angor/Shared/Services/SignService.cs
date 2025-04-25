using System.Reactive.Linq;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;

namespace Angor.Client.Services
{
    public class SignService :  ISignService
    {
        private readonly INostrCommunicationFactory _communicationFactory;
        private readonly INetworkService _networkService;
        private readonly IRelaySubscriptionsHandling _subscriptionsHanding;
        private readonly INostrEventCacheService _nostrEventCacheService;
        private readonly ILogger<SignService> _logger;

        public SignService(
            INostrCommunicationFactory communicationFactory,
            INetworkService networkService,
            IRelaySubscriptionsHandling subscriptionsHanding,
            INostrEventCacheService nostrEventCacheService,
            ILogger<SignService> logger)
        {
            _communicationFactory = communicationFactory;
            _networkService = networkService;
            _subscriptionsHanding = subscriptionsHanding;
            _nostrEventCacheService = nostrEventCacheService;
            _logger = logger;
        }

        public (DateTime,string) RequestInvestmentSigs(string encryptedContent, string investorNostrPrivateKey, string founderNostrPubKey)
        {
            var sender = NostrPrivateKey.FromHex(investorNostrPrivateKey);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedContent,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(founderNostrPubKey),
                    new NostrEventTag("subject","Investment offer"))
            };

            // Blazor does not support AES so it needs to be done manually in javascript
            // var encrypted = ev.EncryptDirect(sender, receiver);
            // var signed = encrypted.Sign(sender);

            var signed = ev.Sign(sender);

            // Store the event in the cache
            var subscriptionKey = founderNostrPubKey + "sig_req";
            _nostrEventCacheService.StoreEvent(subscriptionKey, signed);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return (signed.CreatedAt!.Value, signed.Id);
        }

        public void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey, DateTime? sigRequestSentTime, string sigRequestEventId, Func<string, Task> action)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = projectNostrPubKey;

            // Process cached events first
            var cachedEvents = _nostrEventCacheService.GetCachedEvents(subscriptionKey);
            _logger.LogDebug($"Found {cachedEvents.Count} cached events for subscription {subscriptionKey}");

            foreach (var cachedEvent in cachedEvents
                .Where(e => e.Kind == NostrKind.EncryptedDm)
                .Where(e => e.Tags.FindFirstTagValue("subject") == "Re:Investment offer")
                .Where(e => e.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier) == sigRequestEventId))
            {
                action.Invoke(cachedEvent.Content);
            }

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Re:Investment offer")
                    .Subscribe(_ =>
                    {
                        // Store the event in the cache
                        _nostrEventCacheService.StoreEvent(subscriptionKey, _.Event);
                        action.Invoke(_.Event.Content);
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            // Use the provided sigRequestSentTime or get the latest timestamp from the cache
            var latestTimestamp = sigRequestSentTime ?? _nostrEventCacheService.GetLatestTimestamp(subscriptionKey);

            // Log the request to the relay
            if (latestTimestamp.HasValue)
            {
                _logger.LogInformation("📬 Fetching new investment signature DMs from relay since {Timestamp} for subscription {SubscriptionKey}",
                    latestTimestamp.Value, subscriptionKey);
            }
            else
            {
                _logger.LogInformation("📬 Fetching all investment signature DMs from relay (no timestamp) for subscription {SubscriptionKey}",
                    subscriptionKey);
            }

            _logger.LogDebug("📬 Investment signature request details: EventId={EventId}, From={From}, To={To}",
                sigRequestEventId, projectNostrPubKey, investorNostrPubKey);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = new[] { projectNostrPubKey }, //From founder
                P = new[] { investorNostrPubKey }, // To investor
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = latestTimestamp,
                E = new [] { sigRequestEventId },
                Limit = 1,
            }));
        }

        public Task LookupInvestmentRequestsAsync(string nostrPubKey, string? senderNpub, DateTime? since,
            Action<string, string, string, DateTime> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "sig_req";

            // Process cached events first
            var cachedEvents = _nostrEventCacheService.GetCachedEvents(subscriptionKey);
            _logger.LogDebug($"Found {cachedEvents.Count} cached events for subscription {subscriptionKey}");

            foreach (var cachedEvent in cachedEvents
                .Where(e => e.Tags.FindFirstTagValue("subject") == "Investment offer")
                .Where(e => senderNpub == null || e.Pubkey == senderNpub))
            {
                action.Invoke(cachedEvent.Id, cachedEvent.Pubkey, cachedEvent.Content, cachedEvent.CreatedAt.Value);
            }

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Investment offer")
                    .Select(_ => _.Event)
                    .Subscribe(nostrEvent =>
                    {
                        // Store the event in the cache
                        _nostrEventCacheService.StoreEvent(subscriptionKey, nostrEvent);
                        action.Invoke(nostrEvent.Id, nostrEvent.Pubkey, nostrEvent.Content, nostrEvent.CreatedAt.Value);
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            // Get the latest timestamp from the cache if no specific since parameter is provided
            var latestTimestamp = since ?? _nostrEventCacheService.GetLatestTimestamp(subscriptionKey);

            // Log the request to the relay
            if (latestTimestamp.HasValue)
            {
                _logger.LogInformation("📬 Fetching new DMs from relay since {Timestamp} for subscription {SubscriptionKey}",
                    latestTimestamp.Value, subscriptionKey);
            }
            else
            {
                _logger.LogInformation("📬 Fetching all DMs from relay (no timestamp) for subscription {SubscriptionKey}",
                    subscriptionKey);
            }

            var nostrFilter = new NostrFilter
            {
                P = [nostrPubKey], //To founder,
                Kinds = [NostrKind.EncryptedDm],
                Since = latestTimestamp
            };

            if (senderNpub != null)
            {
                nostrFilter.Authors = [senderNpub]; //From investor
                _logger.LogDebug("📬 Filtering relay request by sender: {SenderNpub}", senderNpub);
            }

            nostrClient.Send(new NostrRequest(subscriptionKey, nostrFilter));

            return Task.CompletedTask;
        }

        public void LookupInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime, string> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "sig_res";

            // Process cached events first
            var cachedEvents = _nostrEventCacheService.GetCachedEvents(subscriptionKey);
            _logger.LogDebug($"Found {cachedEvents.Count} cached events for subscription {subscriptionKey}");

            foreach (var cachedEvent in cachedEvents
                .Where(e => e.Tags.FindFirstTagValue("subject") == "Re:Investment offer"))
            {
                action.Invoke(
                    cachedEvent.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier),
                    cachedEvent.CreatedAt.Value,
                    cachedEvent.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier));
            }

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Re:Investment offer")
                    .Select(_ => _.Event)
                    .Subscribe(nostrEvent =>
                    {
                        // Store the event in the cache
                        _nostrEventCacheService.StoreEvent(subscriptionKey, nostrEvent);
                        action.Invoke(nostrEvent.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier), nostrEvent.CreatedAt.Value, nostrEvent.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier));
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            // Get the latest timestamp from the cache
            var latestTimestamp = _nostrEventCacheService.GetLatestTimestamp(subscriptionKey);

            // Log the request to the relay
            if (latestTimestamp.HasValue)
            {
                _logger.LogInformation("📬 Fetching new approval DMs from relay since {Timestamp} for subscription {SubscriptionKey}",
                    latestTimestamp.Value, subscriptionKey);
            }
            else
            {
                _logger.LogInformation("📬 Fetching all approval DMs from relay (no timestamp) for subscription {SubscriptionKey}",
                    subscriptionKey);
            }

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = new[] { nostrPubKey }, //From founder
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = latestTimestamp
            }));
        }

        public DateTime SendSignaturesToInvestor(string encryptedSignatureInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId)
        {
            var nostrPrivateKey = NostrPrivateKey.FromHex(nostrPrivateKeyHex);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedSignatureInfo,
                Tags = new NostrEventTags(new []
                {
                    NostrEventTag.Profile(investorNostrPubKey),
                    NostrEventTag.Event(eventId),
                    new NostrEventTag("subject","Re:Investment offer"),
                })
            };

            var signed = ev.Sign(nostrPrivateKey);

            // Store the event in the cache
            var subscriptionKey = nostrPrivateKey.DerivePublicKey().Hex + "sig_res";
            _nostrEventCacheService.StoreEvent(subscriptionKey, signed);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return ev.CreatedAt.Value;
        }

        public DateTime SendReleaseSigsToInvestor(string encryptedReleaseSigInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId)
        {
            var nostrPrivateKey = NostrPrivateKey.FromHex(nostrPrivateKeyHex);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedReleaseSigInfo,
                Tags = new NostrEventTags(new[]
                {
                    NostrEventTag.Profile(investorNostrPubKey),
                    NostrEventTag.Event(eventId),
                    new NostrEventTag("subject", "Release transaction signatures"),
                })
            };

            var signed = ev.Sign(nostrPrivateKey);

            // Store the event in the cache
            var projectNostrPubKey = nostrPrivateKey.DerivePublicKey().Hex;
            var subscriptionKey = projectNostrPubKey.Substring(0, 20) + "rel_sigs";
            _nostrEventCacheService.StoreEvent(subscriptionKey, signed);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return ev.CreatedAt.Value;
        }

        public void LookupReleaseSigs(string investorNostrPubKey, string projectNostrPubKey, DateTime? releaseRequestSentTime, string releaseRequestEventId, Action<string> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = projectNostrPubKey.Substring(0, 20) + "rel_sigs";

            // Process cached events first
            var cachedEvents = _nostrEventCacheService.GetCachedEvents(subscriptionKey);
            _logger.LogDebug($"Found {cachedEvents.Count} cached events for subscription {subscriptionKey}");

            foreach (var cachedEvent in cachedEvents
                .Where(e => e.Kind == NostrKind.EncryptedDm)
                .Where(e => e.Tags.FindFirstTagValue("subject") == "Release transaction signatures")
                .Where(e => e.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier) == releaseRequestEventId))
            {
                action.Invoke(cachedEvent.Content);
            }

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Release transaction signatures")
                    .Subscribe(_ =>
                    {
                        // Store the event in the cache
                        _nostrEventCacheService.StoreEvent(subscriptionKey, _.Event);
                        action.Invoke(_.Event.Content);
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            // Use the provided releaseRequestSentTime or get the latest timestamp from the cache
            var latestTimestamp = releaseRequestSentTime ?? _nostrEventCacheService.GetLatestTimestamp(subscriptionKey);

            // Log the request to the relay
            if (latestTimestamp.HasValue)
            {
                _logger.LogInformation("📬 Fetching new release signature DMs from relay since {Timestamp} for subscription {SubscriptionKey}",
                    latestTimestamp.Value, subscriptionKey);
            }
            else
            {
                _logger.LogInformation("📬 Fetching all release signature DMs from relay (no timestamp) for subscription {SubscriptionKey}",
                    subscriptionKey);
            }

            _logger.LogDebug("📬 Release signature request details: EventId={EventId}, From={From}, To={To}",
                releaseRequestEventId, projectNostrPubKey, investorNostrPubKey);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = new[] { projectNostrPubKey }, // From founder
                P = new[] { investorNostrPubKey }, // To investor
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = latestTimestamp,
                E = new[] { releaseRequestEventId },
                Limit = 1,
            }));
        }

        public void LookupSignedReleaseSigs(string projectNostrPubKey, Action<SignServiceLookupItem> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = projectNostrPubKey.Substring(0, 20) + "sing_sigs";

            // Process cached events first
            var cachedEvents = _nostrEventCacheService.GetCachedEvents(subscriptionKey);
            _logger.LogDebug($"Found {cachedEvents.Count} cached events for subscription {subscriptionKey}");

            foreach (var cachedEvent in cachedEvents
                .Where(e => e.Kind == NostrKind.EncryptedDm)
                .Where(e => e.Tags.FindFirstTagValue("subject") == "Release transaction signatures"))
            {
                action.Invoke(new SignServiceLookupItem
                {
                    NostrEvent = cachedEvent,
                    ProfileIdentifier = cachedEvent.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier),
                    EventCreatedAt = cachedEvent.CreatedAt.Value,
                    EventIdentifier = cachedEvent.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier)
                });
            }

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Release transaction signatures")
                    .Select(_ => _.Event)
                    .Subscribe(nostrEvent =>
                    {
                        // Store the event in the cache
                        _nostrEventCacheService.StoreEvent(subscriptionKey, nostrEvent);
                        action.Invoke(new SignServiceLookupItem
                        {
                            NostrEvent = nostrEvent,
                            ProfileIdentifier = nostrEvent.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier),
                            EventCreatedAt = nostrEvent.CreatedAt.Value,
                            EventIdentifier = nostrEvent.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier)
                        });
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            // Get the latest timestamp from the cache
            var latestTimestamp = _nostrEventCacheService.GetLatestTimestamp(subscriptionKey);

            // Log the request to the relay
            if (latestTimestamp.HasValue)
            {
                _logger.LogInformation("📬 Fetching new signed release DMs from relay since {Timestamp} for subscription {SubscriptionKey}",
                    latestTimestamp.Value, subscriptionKey);
            }
            else
            {
                _logger.LogInformation("📬 Fetching all signed release DMs from relay (no timestamp) for subscription {SubscriptionKey}",
                    subscriptionKey);
            }

            _logger.LogDebug("📬 Signed release request details: From={From}", projectNostrPubKey);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilterWithSubject
            {
                Authors = new[] { projectNostrPubKey }, // From founder
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = latestTimestamp
                //Subject =  "Release transaction signatures"
            }));
        }

        public void CloseConnection()
        {
            _subscriptionsHanding.Dispose();
        }
    }

    public class NostrFilterWithSubject : NostrFilter
    {
        /// <summary>A list of subjects to filter by, corresponding to the "subject" tag</summary>
        [JsonProperty("#subject")]
        public string? Subject { get; set; }
    }
}
