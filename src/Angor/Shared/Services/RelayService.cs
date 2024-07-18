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
        private ILogger<RelayService> _logger;
        private readonly INostrCommunicationFactory _communicationFactory;
        private readonly INetworkService _networkService;
        private readonly IRelaySubscriptionsHandling _subscriptionsHandling;
        private readonly ISerializer _serializer;
        
        
        public RelayService(ILogger<RelayService> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService, IRelaySubscriptionsHandling subscriptionsHanding, ISerializer serializer)
        {
            _logger = logger;
            _communicationFactory = communicationFactory;
            _networkService = networkService;
            _subscriptionsHandling = subscriptionsHanding;
            _serializer = serializer;
        }

        public void LookupProjectsInfoByPubKeys<T>(Action<T> responseDataAction, Action? OnEndOfStreamAction,params string[] nostrPubKeys)
        {
            const string subscriptionName = "ProjectInfoLookups";
            
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            
            if (nostrClient == null) 
                throw new InvalidOperationException("The nostr client is null");

            var request = new NostrRequest(subscriptionName, new NostrFilter
            {
                Authors = nostrPubKeys,
                Kinds = new[] { NostrKind.ApplicationSpecificData }
            });

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
            
            nostrClient.Send(request);
        }

        public void RequestProjectCreateEventsByPubKey(Action<NostrEvent> onResponseAction, Action? onEoseAction,params string[] nostrPubKeys)
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
                Authors = nostrPubKeys,
                Kinds = new[] { NostrKind.ApplicationSpecificData, NostrKind.Metadata},
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
                A = new []{ NostrCoordinatesIdentifierTag(nostrPubKey)},
                Since = since,
                Limit = limit
            }));
            
            return Task.CompletedTask;
        }

        public Task LookupDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit, Func<NostrEvent,Task> onResponseAction, string? senderNpub = null)
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

                _subscriptionsHandling.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            var nostrFilter = new NostrFilter
            {
                P = new[] { nostrPubKey },
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = since,
                Limit = limit
            };

            if (senderNpub != null)
                nostrFilter.Authors = new[] { senderNpub };

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

        private string NostrCoordinatesIdentifierTag(string nostrPubKey)
        {
            return $"{(int)NostrKind.ApplicationSpecificData}:{nostrPubKey}:AngorApp";
        }

        public void CloseConnection()
        {
            _subscriptionsHandling.Dispose();
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

        public Task<string> CreateNostrProfileAsync(NostrMetadata metadata, string hexPrivateKey,Action<NostrOkResponse> action)
        {
            var key = NostrPrivateKey.FromHex(hexPrivateKey);

            var content = _serializer.Serialize(metadata);
            
            var signed = new NostrEvent
                {
                    Kind = NostrKind.Metadata,
                    CreatedAt = DateTime.UtcNow,
                    Content = content,
                    Tags = new NostrEventTags( //TODO need to find the correct tags for the event
                        new NostrEventTag("d", "AngorApp", "Create a new project event"),
                        new NostrEventTag("L", "#projectInfo"),
                        new NostrEventTag("l", "ProjectDeclaration", "#projectInfo"))
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
                Content = content,
                Tags = new NostrEventTags( //TODO need to find the correct tags for the event
                    new NostrEventTag("d", "AngorApp", "Create a new project event"),
                    new NostrEventTag("L", "#projectInfo"),
                    new NostrEventTag("l", "ProjectDeclaration", "#projectInfo"))
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
