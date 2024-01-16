using System.Reactive.Linq;
using System.Text.Json;
using Angor.Shared.Models;
using Angor.Shared.Utilities;
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
        private INostrCommunicationFactory _communicationFactory;
        private INetworkService networkService;
        private IRelaySubscriptionsHandling _subscriptionsHanding;
        
        
        public RelayService(ILogger<RelayService> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService,ILogger<RelaySubscriptionsHandling> baseLogger, IRelaySubscriptionsHandling subscriptionsHanding)
        {
            _logger = logger;
            _communicationFactory = communicationFactory;
            this.networkService = networkService;
            _subscriptionsHanding = subscriptionsHanding;

            var nostrClient = _communicationFactory.GetOrCreateClient(this.networkService);
            
            nostrClient.Streams.OkStream.Subscribe(_subscriptionsHanding.HandleOkMessages);
            nostrClient.Streams.EoseStream.Subscribe(_subscriptionsHanding.HandleEoseMessages);
        }

        public void RegisterOKMessageHandler(string eventId, Action<NostrOkResponse> action)
        {
            //TODO add this for every call
           // _subscriptionsHanding. OkVerificationActions.Add(eventId,new SubscriptionCallCounter<Action<NostrOkResponse>>(action));
        }

        public void LookupProjectsInfoByPubKeys<T>(Action<T> responseDataAction, Action? OnEndOfStreamAction,params string[] nostrPubKeys)
        {
            const string subscriptionName = "ProjectInfoLookups";
            
            var nostrClient = _communicationFactory.GetOrCreateClient(networkService);
            
            if (nostrClient == null) 
                throw new InvalidOperationException("The nostr client is null");

            var request = new NostrRequest(subscriptionName, new NostrFilter
            {
                Authors = nostrPubKeys,
                Kinds = new[] { NostrKind.ApplicationSpecificData }
            });

            // if (!relaySubscriptions.ContainsKey(subscriptionName))
            // {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionName)
                    .Select(_ => _.Event)
                    .Subscribe(ev =>
                    {
                        responseDataAction(JsonSerializer.Deserialize<T>(ev.Content,settings));
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionName, subscription);
            //}

            if (OnEndOfStreamAction != null)
            {
                _subscriptionsHanding.TryAddEoseAction(subscriptionName, OnEndOfStreamAction);
            }
            
            nostrClient.Send(request);
        }

        public void RequestProjectCreateEventsByPubKey(Action<NostrEvent> onResponseAction, Action? onEoseAction,params string[] nostrPubKeys)
        {
            var subscriptionKey = Guid.NewGuid().ToString().Replace("-","");
            
            var nostrClient = _communicationFactory.GetOrCreateClient(networkService);
            
            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = nostrPubKeys,
                Kinds = new[] { NostrKind.ApplicationSpecificData, NostrKind.Metadata},
            }));

            var subscription = nostrClient.Streams.EventStream
                .Where(_ => _.Subscription == subscriptionKey)
                .Where(_ => _.Event is not null)
                .Select(_ => _.Event)
                .Subscribe(onResponseAction!);

            _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);

            if (onEoseAction != null)
            {
                _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onEoseAction);   
            }
        }

        public Task LookupDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit, Action<NostrEvent> onResponseAction)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(networkService);

            var subscriptionKey = nostrPubKey + "DM";

            // if (!relaySubscriptions.ContainsKey(subscriptionKey))
            // {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event is not null)
                    .Select(_ => _.Event)
                    .Subscribe(onResponseAction!);
                
                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
                // }

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
        
        private string NostrCoordinatesIdentifierTag(string nostrPubKey)
        {
            return $"{(int)NostrKind.ApplicationSpecificData}:{nostrPubKey}:AngorApp";
        }

        public void CloseConnection()
        {
            _subscriptionsHanding.Dispose();
        }

        public Task<string> AddProjectAsync(ProjectInfo project, string hexPrivateKey)
        {
            var key = NostrPrivateKey.FromHex(hexPrivateKey);

            if (!project.NostrPubKey.Contains(key.DerivePublicKey().Hex))
                throw new ArgumentException($"The nostr pub key on the project does not fit the npub calculated from the nsec {project.NostrPubKey} {key.DerivePublicKey().Hex}");
            
            var content = JsonSerializer.Serialize(project,settings);
            
            var signed = GetNip78NostrEvent(content)
                .Sign(key);

            var nostrClient = _communicationFactory.GetOrCreateClient(networkService);
            
            nostrClient.Send(new NostrEventRequest(signed));
            
            return Task.FromResult(signed.Id);
        }

        public Task<string> CreateNostrProfileAsync(NostrMetadata metadata, string hexPrivateKey)
        {
            var key = NostrPrivateKey.FromHex(hexPrivateKey);

            var content = JsonSerializer.Serialize(metadata,settings);
            
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

            var nostrClient = _communicationFactory.GetOrCreateClient(networkService);
            
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

            var nostrClient = _communicationFactory.GetOrCreateClient(networkService);
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
        
        public static JsonSerializerOptions settings =>  new ()
        {
            // Equivalent to Formatting = Formatting.None
            WriteIndented = false,

            // Equivalent to NullValueHandling = NullValueHandling.Ignore
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,

            // PropertyNamingPolicy equivalent to CamelCasePropertyNamesContractResolver
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

            Converters = { new UnixDateTimeConverter() }
        };
    }

}
