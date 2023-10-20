using System.Reactive.Linq;
using Angor.Shared.Models;
using Nostr.Client.Requests;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Responses;

namespace Angor.Shared.Services
{
    public interface IRelayService
    {
        Task ConnectToRelaysAsync();

        void RegisterEventMessageHandler<T>(string eventId,Action<T> action);
        void RegisterOKMessageHandler(Action<NostrOkResponse> action);
        
        Task<string> AddProjectAsync(ProjectInfo project, string nsec);

        Task RequestProjectDataAsync<T>(Action<T> responseDataAction,params string[] nostrPubKey);

        void CloseConnection();
    }

    public class RelayService : IRelayService
    {
        private static NostrWebsocketClient _nostrClient;
        private static INostrCommunicator _nostrCommunicator;
        
        private ILogger<RelayService> _logger;

        private ILogger<NostrWebsocketClient> _clientLogger; 
        private ILogger<NostrWebsocketCommunicator> _communicatorLogger;

        private Dictionary<string, IDisposable> subscriptions = new();

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
            if (_nostrCommunicator != null)
                return;

            _nostrCommunicator = new NostrWebsocketCommunicator(new Uri("ws://angor-relay.test"));

            _nostrCommunicator.Name = "angor-relay.test";
            _nostrCommunicator.ReconnectTimeout = null; //TODO need to check what is the actual best time to set here
            
            _nostrCommunicator.DisconnectionHappened.Subscribe(info =>
            {
                _communicatorLogger.LogError(info.Exception, "Relay disconnected, type: {Type}, reason: {CloseStatus}.", info.Type, info.CloseStatus);
            });
            
            _nostrCommunicator.MessageReceived.Subscribe(info =>
            {
                _communicatorLogger.LogInformation("message received on communicator - {Text} Relay message received, type: {MessageType}",info.Text, info.MessageType);
            });
            
            await _nostrCommunicator.StartOrFail();

            _nostrClient = new NostrWebsocketClient(_nostrCommunicator, _clientLogger);

            _nostrClient.Streams.UnknownMessageStream.Subscribe(_ => _clientLogger.LogInformation($"UnknownMessageStream {_}",_.MessageType));
            _nostrClient.Streams.EventStream.Subscribe(_ => _clientLogger.LogInformation($"EventStream {_.Subscription}", _.AdditionalData));
            _nostrClient.Streams.EoseStream.Subscribe(_ => _clientLogger.LogInformation($"EoseStream on subscription - {_.Subscription}", _.AdditionalData));
            
            _nostrClient.Streams.OkStream.Subscribe(_ => _clientLogger.LogInformation($"OkStream {_.Accepted} message - {_.Message}"));
            
            _nostrClient.Streams.EoseStream.Subscribe(_ =>
            {
                if (!subscriptions.ContainsKey(_.Subscription)) 
                    return;
                _clientLogger.LogInformation($"Disposing of subscription - {_.Subscription}");
                subscriptions[_.Subscription].Dispose();
                subscriptions.Remove(_.Subscription);
                _clientLogger.LogInformation($"subscription disposed - {_.Subscription}");
            });
            
            _nostrClient.Streams.NoticeStream.Subscribe(_ => _clientLogger.LogInformation($"NoticeStream {_.Message}"));
            _nostrClient.Streams.UnknownRawStream.Subscribe(_ => _clientLogger.LogInformation($"UnknownRawStream {_.Message}"));
        }

        public void RegisterEventMessageHandler<T>(string eventId ,Action<T> action)
        {
            // var subscription = _nostrClient.Streams.EventStream
            //     .Where(_ => _.Subscription == "ProjectInfoLookups")
            //     .Where(_ => _.Event.Id == eventId)
            //     .Select(_ => _.Event)
            //     .Subscribe(ev =>
            //     {
            //         action(Newtonsoft.Json.JsonConvert.DeserializeObject<T>(ev.Content));
            //     });
            //
            // subscriptions.Add(eventId,subscription);
        }
        
        public void RegisterOKMessageHandler(Action<NostrOkResponse> action)
        {
            var subscription =_nostrClient.Streams.OkStream
                .Subscribe(ev => { action(ev); });
            
            subscriptions.Add("todo",subscription);
        }

        public Task RequestProjectDataAsync<T>(Action<T> responseDataAction,params string[] nostrPubKeys)
        {
            string subscriptionName = "ProjectInfoLookups";
            _nostrClient.Send(new NostrRequest(subscriptionName, new NostrFilter
            {
                Authors = nostrPubKeys,
                Kinds = new[] { NostrKind.ApplicationSpecificData, NostrKind.Metadata, (NostrKind)30402 },
            }));

            if (!subscriptions.ContainsKey(subscriptionName))
            {
                var subscription = _nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionName)
                    .Where(_ => nostrPubKeys.Contains(_.Event.Pubkey))
                    .Select(_ => _.Event)
                    .Subscribe(ev =>
                    {
                        responseDataAction(Newtonsoft.Json.JsonConvert.DeserializeObject<T>(ev.Content));
                    });

                subscriptions.Add(subscriptionName, subscription);
            }

            return Task.CompletedTask;
        }

        public void CloseConnection()
        {
            foreach (var subscription in subscriptions.Values)
            {
                subscription.Dispose();
            }
            _nostrClient?.Dispose();
            _nostrCommunicator?.Dispose();
        }

        public Task<string> AddProjectAsync(ProjectInfo project, string hexPrivateKey)
        {
            var content = Newtonsoft.Json.JsonConvert.SerializeObject(project);

            var ev = new NostrEvent
            {
                Kind = NostrKind.ApplicationSpecificData,
                CreatedAt = DateTime.UtcNow,
                Content = content,
                Pubkey = project.NostrPubKey,
                Tags = new NostrEventTags(//TODO need to find the correct tags for the event
                    new NostrEventTag("d", "AngorApp", "Create a new project event"),
                    new NostrEventTag("L", "#projectInfo"),
                    new NostrEventTag("l", "ProjectDeclaration", "#projectInfo")) 
            };
            
            var key = NostrPrivateKey.FromHex(hexPrivateKey);
            var signed = ev.Sign(key);

            _nostrClient.Send(new NostrEventRequest(signed));

            return Task.FromResult(ev.Id);
        }
    }

}
