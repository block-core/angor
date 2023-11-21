using System.Reactive.Linq;
using System.Text.Json;
using Angor.Shared.Models;
using Angor.Shared.Utilities;
using Nostr.Client.Requests;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Metadata;
using Nostr.Client.Responses;

namespace Angor.Shared.Services
{
    public class RelayService : IRelayService
    {
        private static NostrWebsocketClient? _nostrClient;
        private static INostrCommunicator? _nostrCommunicator;
        
        private ILogger<RelayService> _logger;

        private ILogger<NostrWebsocketClient> _clientLogger; 
        private ILogger<NostrWebsocketCommunicator> _communicatorLogger;

        private Dictionary<string, IDisposable> userSubscriptions = new();
        private Dictionary<string, IDisposable> userEoseSubscriptions = new();
        private List<IDisposable> serviceSubscriptions = new();
        private Dictionary<string, Action<NostrOkResponse>> OkVerificationActions = new();

        public RelayService(
            ILogger<RelayService> logger, 
            ILogger<NostrWebsocketClient> clientLogger, 
            ILogger<NostrWebsocketCommunicator> communicatorLogger)
        {
            _logger = logger;
            _clientLogger = clientLogger;
            _communicatorLogger = communicatorLogger;
        }

        public async Task ConnectToRelaysAsync()
        {
            if (_nostrCommunicator == null)
            {
                SetupNostrCommunicator();
            }

            if (_nostrClient == null)
            {
                SetupNostrClient();
            }
            
            await _nostrCommunicator.StartOrFail();
        }

        public void RegisterOKMessageHandler(string eventId, Action<NostrOkResponse> action)
        {
            OkVerificationActions.Add(eventId,action);
        }

        public Task LookupProjectsInfoByPubKeysAsync<T>(Action<T> responseDataAction,params string[] nostrPubKeys)
        {
            const string subscriptionName = "ProjectInfoLookups";
            
            if (_nostrClient == null) 
                throw new InvalidOperationException("The nostr client is null");

            var request = new NostrRequest(subscriptionName, new NostrFilter
            {
                Authors = nostrPubKeys,
                Kinds = new[] { NostrKind.ApplicationSpecificData }, //, NostrKind.Metadata, (NostrKind)30402 },
            });

            _nostrClient.Send(request);

            if (!userSubscriptions.ContainsKey(subscriptionName))
            {
                var subscription = _nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionName)
                    .Select(_ => _.Event)
                    .Subscribe(ev =>
                    {
                        responseDataAction(JsonSerializer.Deserialize<T>(ev.Content,settings));
                    });

                userSubscriptions.Add(subscriptionName, subscription);
            }

            return Task.CompletedTask;
        }

        public Task RequestProjectCreateEventsByPubKeyAsync(string nostrPubKey, Action<NostrEvent> onResponseAction)
        {
            if (_nostrClient == null) throw new InvalidOperationException("The nostr client is null");
            _nostrClient.Send(new NostrRequest(nostrPubKey, new NostrFilter
            {
                Authors = new []{nostrPubKey},
                Kinds = new[] { NostrKind.ApplicationSpecificData, NostrKind.Metadata},
            }));

            if (!userSubscriptions.ContainsKey(nostrPubKey))
            {
                var subscription = _nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == nostrPubKey)
                    .Where(_ => _.Event is not null)
                    .Select(_ => _.Event)
                    .Subscribe(onResponseAction!);

                userSubscriptions.Add(nostrPubKey, subscription);
            }

            return Task.CompletedTask;
        }

        public Task LookupDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit, Action<NostrEvent> onResponseAction)//, Action<NostrEoseResponse> onEoseAction)
        {
            if (_nostrClient == null) 
                throw new InvalidOperationException("The nostr client is null");

            var subscriptionKey = nostrPubKey + "DM";
            
            _nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                P = new[] { nostrPubKey },
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = since,
                Limit = limit
            }));

            if (!userSubscriptions.ContainsKey(subscriptionKey))
            {
                var subscription = _nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event is not null)
                    .Select(_ => _.Event)
                    .Subscribe(onResponseAction!);

                userSubscriptions.Add(subscriptionKey, subscription);
            }
            
            // if (!userEoseSubscriptions.ContainsKey(subscriptionKey))
            // {
            //     var subscriptionEOSE = _nostrClient.Streams.EoseStream
            //         .Where(_ => _.Subscription == subscriptionKey)
            //         .Subscribe(onEoseAction);
            //
            //     userEoseSubscriptions.Add(subscriptionKey, subscriptionEOSE);
            // }

            return Task.CompletedTask;
        }

        public void CloseConnection()
        {
            userSubscriptions.Values.ToList().ForEach(_ => _.Dispose());
            userEoseSubscriptions.Values.ToList().ForEach(_ => _.Dispose());
            serviceSubscriptions.ForEach(_ => _.Dispose());
            _nostrClient?.Dispose();
            _nostrCommunicator?.Dispose();
            _nostrClient = null;
            _nostrCommunicator = null;
        }

        public Task<string> AddProjectAsync(ProjectInfo project, string hexPrivateKey)
        {
            var key = NostrPrivateKey.FromHex(hexPrivateKey);

            if (!project.NostrPubKey.Contains(key.DerivePublicKey().Hex))
                throw new ArgumentException($"The nostr pub key on the project does not fit the npub calculated from the nsec {project.NostrPubKey} {key.DerivePublicKey().Hex}");
            
            var content = JsonSerializer.Serialize(project,settings);
            
            var signed = GetNip78NostrEvent(content)
                .Sign(key);

            if (_nostrClient == null)
                throw new InvalidOperationException();
            
            _nostrClient.Send(new NostrEventRequest(signed));
            
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

            if (_nostrClient == null)
                throw new InvalidOperationException();
            
            _nostrClient.Send(new NostrEventRequest(signed));
            
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

            _nostrClient.Send(deleteEvent);
            
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
        
        
        private void SetupNostrClient()
        {
            _nostrClient = new NostrWebsocketClient(_nostrCommunicator, _clientLogger);
            
            serviceSubscriptions.Add(_nostrClient.Streams.UnknownMessageStream.Subscribe(_ => _clientLogger.LogError($"UnknownMessageStream {_.MessageType} {_.AdditionalData}")));
            serviceSubscriptions.Add(_nostrClient.Streams.EventStream.Subscribe(_ => _clientLogger.LogInformation($"EventStream {_.Subscription} {_.AdditionalData}")));
            serviceSubscriptions.Add(_nostrClient.Streams.NoticeStream.Subscribe(_ => _clientLogger.LogError($"NoticeStream {_.Message}")));
            serviceSubscriptions.Add(_nostrClient.Streams.UnknownRawStream.Subscribe(_ => _clientLogger.LogError($"UnknownRawStream {_.Message}")));
            
            serviceSubscriptions.Add( _nostrClient.Streams.OkStream.Subscribe(_ =>
            {
                _clientLogger.LogInformation($"OkStream {_.Accepted} message - {_.Message}");

                if (_.EventId != null && OkVerificationActions.ContainsKey(_.EventId))
                {
                    OkVerificationActions[_.EventId](_);
                    OkVerificationActions.Remove(_.EventId);
                }
            }));

            serviceSubscriptions.Add(_nostrClient.Streams.EoseStream.Subscribe(_ =>
            {
                _clientLogger.LogInformation($"EoseStream {_.Subscription} message - {_.AdditionalData}");
                
                if (!userSubscriptions.ContainsKey(_.Subscription))
                    return;
                
                _clientLogger.LogInformation($"Disposing of subscription - {_.Subscription}");
                _nostrClient.Send(new NostrCloseRequest(_.Subscription));
                userSubscriptions[_.Subscription].Dispose();
                userSubscriptions.Remove(_.Subscription);
                _clientLogger.LogInformation($"subscription disposed - {_.Subscription}");
            }));
        }

        private void SetupNostrCommunicator()
        {
            _nostrCommunicator = new NostrWebsocketCommunicator(new Uri("wss://relay.angor.io"))
            {
                Name = "angor-relay.test",
                ReconnectTimeout = null //TODO need to check what is the actual best time to set here
            };

            serviceSubscriptions.Add(_nostrCommunicator.DisconnectionHappened.Subscribe(info =>
            {
                if (info.Exception != null)
                    _communicatorLogger.LogError(info.Exception,
                        "Relay disconnected, type: {Type}, reason: {CloseStatus}", info.Type,
                        info.CloseStatusDescription);
                else
                    _communicatorLogger.LogInformation("Relay disconnected, type: {Type}, reason: {CloseStatus}",
                        info.Type, info.CloseStatusDescription);
            }));

            serviceSubscriptions.Add(_nostrCommunicator.MessageReceived.Subscribe(info =>
            {
                _communicatorLogger.LogInformation(
                    "message received on communicator - {Text} Relay message received, type: {MessageType}",
                    info.Text, info.MessageType);
            }));
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
