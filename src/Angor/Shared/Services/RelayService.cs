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
        
        private Dictionary<string, SubscriptionCallCounter<IDisposable>> userSubscriptions = new();
        private Dictionary<string, SubscriptionCallCounter<Action>> userEoseActions = new();
        private readonly List<IDisposable> _serviceSubscriptions;
        private Dictionary<string, SubscriptionCallCounter<Action<NostrOkResponse>>> OkVerificationActions = new();

        private class SubscriptionCallCounter<T>
        {
            public SubscriptionCallCounter(T item)
            {
                Item = item;
            }

            public int NumberOfInvocations { get; set; }
            public T Item { get; }
        }
        
        public RelayService(ILogger<RelayService> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService)
        {
            _logger = logger;
            _communicationFactory = communicationFactory;
            this.networkService = networkService;

            var nostrClient = _communicationFactory.CreateClient(this.networkService);
            
            _serviceSubscriptions = new();            
            _serviceSubscriptions.Add( nostrClient.Streams.OkStream.Subscribe(_ =>
            {
                _logger.LogInformation($"OkStream {_.Accepted} message - {_.Message}");

                if (OkVerificationActions.TryGetValue(_?.EventId ?? string.Empty, out SubscriptionCallCounter<Action<NostrOkResponse>> value))
                {
                    value.NumberOfInvocations++;
                    value.Item(_);
                    if (value.NumberOfInvocations == _communicationFactory.GetNumberOfRelaysConnected())
                    {
                        OkVerificationActions.Remove(_.EventId ?? string.Empty);
                    }
                }
            }));
            
            _serviceSubscriptions.Add(nostrClient.Streams.EoseStream.Subscribe(_ =>
            {
                _logger.LogInformation($"EoseStream {_.Subscription} message - {_.AdditionalData}");

                if (userEoseActions.TryGetValue(_.Subscription, out SubscriptionCallCounter<Action> value))
                {
                    value.NumberOfInvocations++;
                    if (userEoseActions[_.Subscription].NumberOfInvocations == _communicationFactory.GetNumberOfRelaysConnected())
                    {
                        _logger.LogInformation($"Invoking action on EOSE - {_.Subscription}");
                        value.Item.Invoke();
                        userEoseActions.Remove(_.Subscription);
                        _logger.LogInformation($"Removed action on EOSE for subscription - {_.Subscription}");   
                    }
                }

                if (!userSubscriptions.ContainsKey(_.Subscription)) 
                    return;

                userSubscriptions[_.Subscription].NumberOfInvocations++;
                
                if (userSubscriptions[_.Subscription].NumberOfInvocations != _communicationFactory.GetNumberOfRelaysConnected()) 
                    return;
                
                _logger.LogInformation($"Disposing of subscription - {_.Subscription}");
                nostrClient.Send(new NostrCloseRequest(_.Subscription));
                userSubscriptions[_.Subscription].Item.Dispose();
                userSubscriptions.Remove(_.Subscription);
                _logger.LogInformation($"subscription disposed - {_.Subscription}");
            }));
            
        }

        public void RegisterOKMessageHandler(string eventId, Action<NostrOkResponse> action)
        {
            OkVerificationActions.Add(eventId,new SubscriptionCallCounter<Action<NostrOkResponse>>(action));
        }

        public void LookupProjectsInfoByPubKeys<T>(Action<T> responseDataAction, Action? OnEndOfStreamAction,params string[] nostrPubKeys)
        {
            const string subscriptionName = "ProjectInfoLookups";
            
            var nostrClient = _communicationFactory.CreateClient(networkService);
            
            if (nostrClient == null) 
                throw new InvalidOperationException("The nostr client is null");

            var request = new NostrRequest(subscriptionName, new NostrFilter
            {
                Authors = nostrPubKeys,
                Kinds = new[] { NostrKind.ApplicationSpecificData }
            });

            nostrClient.Send(request);

            if (!userSubscriptions.ContainsKey(subscriptionName))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionName)
                    .Select(_ => _.Event)
                    .Subscribe(ev =>
                    {
                        responseDataAction(JsonSerializer.Deserialize<T>(ev.Content,settings));
                    });

                userSubscriptions.Add(subscriptionName, new SubscriptionCallCounter<IDisposable>(subscription));
            }

            if (OnEndOfStreamAction != null)
            {
                userEoseActions.Add(subscriptionName,new SubscriptionCallCounter<Action>(OnEndOfStreamAction));
            }
        }

        public void RequestProjectCreateEventsByPubKey(Action<NostrEvent> onResponseAction, Action? onEoseAction,params string[] nostrPubKeys)
        {
            var subscriptionKey = Guid.NewGuid().ToString().Replace("-","");
            
            var nostrClient = _communicationFactory.CreateClient(networkService);
            
            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = nostrPubKeys,
                Kinds = new[] { NostrKind.ApplicationSpecificData, NostrKind.Metadata},
            }));

            if (userSubscriptions.ContainsKey(subscriptionKey)) 
                return;
            
            var subscription = nostrClient.Streams.EventStream
                .Where(_ => _.Subscription == subscriptionKey)
                .Where(_ => _.Event is not null)
                .Select(_ => _.Event)
                .Subscribe(onResponseAction!);

            userSubscriptions.Add(subscriptionKey, new SubscriptionCallCounter<IDisposable>(subscription));

            userEoseActions.TryAdd(subscriptionKey, new SubscriptionCallCounter<Action>(onEoseAction));
        }

        public Task LookupDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit, Action<NostrEvent> onResponseAction)
        {
            var nostrClient = _communicationFactory.CreateClient(networkService);

            var subscriptionKey = nostrPubKey + "DM";
            
            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                P = new[] { nostrPubKey },
                Kinds = new[] { NostrKind.EncryptedDm },
                A = new []{ NostrCoordinatesIdentifierTag(nostrPubKey)},
                Since = since,
                Limit = limit
            }));

            if (!userSubscriptions.ContainsKey(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event is not null)
                    .Select(_ => _.Event)
                    .Subscribe(onResponseAction!);

                userSubscriptions.Add(subscriptionKey, new SubscriptionCallCounter<IDisposable>(subscription));
            }

            return Task.CompletedTask;
        }
        
        private string NostrCoordinatesIdentifierTag(string nostrPubKey)
        {
            return $"{(int)NostrKind.ApplicationSpecificData}:{nostrPubKey}:AngorApp";
        }

        public void CloseConnection()
        {
            userSubscriptions.Values.ToList().ForEach(_ => _.Item.Dispose());
            _serviceSubscriptions.ForEach(subscription => subscription.Dispose());
            _communicationFactory.Dispose();
            
        }

        public Task<string> AddProjectAsync(ProjectInfo project, string hexPrivateKey)
        {
            var key = NostrPrivateKey.FromHex(hexPrivateKey);

            if (!project.NostrPubKey.Contains(key.DerivePublicKey().Hex))
                throw new ArgumentException($"The nostr pub key on the project does not fit the npub calculated from the nsec {project.NostrPubKey} {key.DerivePublicKey().Hex}");
            
            var content = JsonSerializer.Serialize(project,settings);
            
            var signed = GetNip78NostrEvent(content)
                .Sign(key);

            var nostrClient = _communicationFactory.CreateClient(networkService);
            
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

            var nostrClient = _communicationFactory.CreateClient(networkService);
            
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

            var nostrClient = _communicationFactory.CreateClient(networkService);
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
