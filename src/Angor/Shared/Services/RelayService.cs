using Angor.Shared.Models;
using Microsoft.Extensions.Logging;
using Nostr.Client.Json;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Contacts;
using Nostr.Client.Messages.Metadata;
using Nostr.Client.Requests;
using Nostr.Client.Responses;
using System.Reactive.Linq;

namespace Angor.Shared.Services
{
    public class RelayService : IRelayService
    {
        private ILogger<RelayService> _logger;
        private readonly INostrCommunicationFactory _communicationFactory;
        private readonly INetworkService _networkService;
        private readonly IRelaySubscriptionsHandling _subscriptionsHandling;
        private readonly ISerializer _serializer;

        // https://github.com/block-core/nips/blob/peer-to-peer-decentralized-funding/3030.md
        public const NostrKind Nip3030NostrKind = (NostrKind)3030;

        public RelayService(ILogger<RelayService> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService, IRelaySubscriptionsHandling subscriptionsHanding, ISerializer serializer)
        {
            _logger = logger;
            _communicationFactory = communicationFactory;
            _networkService = networkService;
            _subscriptionsHandling = subscriptionsHanding;
            _serializer = serializer;
        }

        public void DisconnectSubscription(string subscription)
        {
            _subscriptionsHandling.CloseSubscription(subscription);
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
                Kinds = [Nip3030NostrKind, NostrKind.ApplicationSpecificData, NostrKind.Metadata],
            }));
        }

        public Task LookupSignaturesDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit, Action<NostrEvent> onResponseAction)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            var subscriptionKey = nostrPubKey + "DM";

            if (!_subscriptionsHandling.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event is not null)
                    .Select(_ => _.Event)
                    .Subscribe(onResponseAction!);

                _subscriptionsHandling.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                P = new[] { nostrPubKey },
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = since,
                Limit = limit
            }));
            
            return Task.CompletedTask;
        }

        public Task LookupDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit, Func<NostrEvent,Task> onResponseAction, string[]? sendersPubkey = null, bool keepActive = false)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            var subscriptionKey = nostrPubKey;

            if (!_subscriptionsHandling.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event is not null)
                    .Select(_ => _.Event)
                    .Subscribe(@event => onResponseAction(@event));

                _subscriptionsHandling.TryAddRelaySubscription(subscriptionKey, subscription, keepActive);
            }

            var nostrFilter = new NostrFilter
            {
                P = new[] { nostrPubKey },
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = since,
                Limit = limit
            };

            if (sendersPubkey != null)
                nostrFilter.Authors = sendersPubkey;

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
            
            var signed = GetNip3030NostrEvent(content)
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

        public Task<string> CreateNostrRelayNip65Async(string[] relays, string hexPrivateKey, Action<NostrOkResponse> action)
        {
            Nostr.Client.Messages.Contacts.NostrRelays relayList = new NostrRelays(relays.ToDictionary(a => a, s => new NostrRelayConfig()));
            
            var key = NostrPrivateKey.FromHex(hexPrivateKey);

            var content = _serializer.Serialize(relayList);

            var signed = new NostrEvent
            {
                Kind = NostrKind.Metadata,
                CreatedAt = DateTime.UtcNow,
                Content = content
            }.Sign(key);

            var signed1 = new NostrContactEvent(content)
            {
                Kind = NostrKind.RelayListMetadata,
                CreatedAt = DateTime.UtcNow,
            }.Sign(key);

            _subscriptionsHandling.TryAddOKAction(signed.Id, action);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            nostrClient.Send(new NostrEventRequest(signed));

            return Task.FromResult(signed.Id);
        }

        private static NostrEvent GetNip3030NostrEvent( string content)
        {
            // https://github.com/block-core/nips/blob/peer-to-peer-decentralized-funding/3030.md

            var ev = new NostrEvent
            {
                //Kind = NostrKind.ApplicationSpecificData,
                Kind = Nip3030NostrKind,
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
