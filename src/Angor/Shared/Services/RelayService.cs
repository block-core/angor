using System.Reactive.Linq;
using Angor.Client.Services;
using Angor.Shared.Models;
using Nostr.Client.Messages.Metadata;
using Nostr.Client.Requests;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Keys;
using Nostr.Client.Messages;

namespace Angor.Shared.Services
{
    public interface IRelayService
    {
        Task ConnectToRelaysAsync();
        
        Task AddProjectAsync(ProjectInfo project, string nsec);
        Task<ProjectInfo?> GetProjectAsync(string projectId);

        Task RequestProjectDataAsync(string nostrPubKey);
    }

    public class RelayService : IRelayService
    {
        
        private static INostrClient _nostrClient;
        private static INostrCommunicator _nostrCommunicator;

        private readonly ISessionStorage _storage;
        private ILogger<RelayService> _logger;

        private ILogger<NostrWebsocketClient> _clientLogger; 
        
        public RelayService(ISessionStorage storage, ILogger<RelayService> logger, 
            ILogger<NostrWebsocketClient> clientLogger)
        {
            _storage = storage;
            _logger = logger;
            //_nostrCommunicator = nostrCommunicator;
            _clientLogger = clientLogger;
        }

        public async Task ConnectToRelaysAsync()
        {
            if (_nostrCommunicator != null)
                return;

            _nostrCommunicator = new NostrWebsocketCommunicator(new Uri("ws://angor-relay.test"));

            _nostrCommunicator.Name = "angor-relay.test";
            _nostrCommunicator.ReconnectTimeout = null;
            
            _nostrCommunicator.DisconnectionHappened.Subscribe(info =>
            {
                _logger.LogError(info.Exception, "Relay disconnected, type: {type}, reason: {reason}.", info.Type, info.CloseStatus);
                _nostrCommunicator.Start();
            });
            _nostrCommunicator.MessageReceived.Subscribe(info => _logger.LogInformation(info.Text, "Relay message received, type: {type}", info.MessageType));
            
            await _nostrCommunicator.StartOrFail();
            
            _nostrClient = new NostrWebsocketClient(_nostrCommunicator, _clientLogger);

            _nostrClient.Send(new NostrRequest("default",new NostrFilter { Kinds = new[] { NostrKind.Metadata } }));
            
            _nostrClient.Streams.UnknownMessageStream.Subscribe(_ => _logger.LogInformation($"UnknownMessageStream {_}",_.MessageType));
            _nostrClient.Streams.EventStream.Subscribe(_ => _logger.LogInformation($"EventStream {_.Subscription}", _.AdditionalData));
            _nostrClient.Streams.EoseStream.Subscribe(_ => _logger.LogInformation($"EoseStream on subscription - {_.Subscription}", _.AdditionalData));
            _nostrClient.Streams.OkStream.Subscribe(_ => _logger.LogInformation($"OkStream {_}", _.MessageType));
            _nostrClient.Streams.NoticeStream.Subscribe(_ => _logger.LogInformation($"NoticeStream {_}", _.MessageType));
            _nostrClient.Streams.UnknownRawStream.Subscribe(_ => _logger.LogInformation($"UnknownRawStream {_}", _.Message.MessageType.ToString()));
        }
        
        public Task RequestProjectDataAsync(string nostrPubKey)
        {
            if (_storage.IsProjectInSubscribedList(nostrPubKey))
                return Task.CompletedTask;

            _nostrClient.Send( new NostrRequest(nostrPubKey, new NostrFilter
                { Authors = new[] { nostrPubKey }, Kinds = new[] { NostrKind.Metadata } }));

            _nostrClient.Streams.EventStream.Where(_ => _.Subscription == nostrPubKey)
                .Select(_ => _.Event)
                .Subscribe(ev =>
                {
                    _logger.LogInformation("{kind}: {content}", ev?.Kind, ev?.Content);

                    if (ev is not NostrMetadataEvent evm)
                        return;

                    var projectInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectInfo>(evm.Content);
                    _storage.StoreProjectInfo(projectInfo);
                    _logger.LogInformation($"Updated storage with project from nostr {projectInfo.ProjectIdentifier}");
                });

            _storage.AddProjectToSubscribedList(nostrPubKey);
            
            return Task.CompletedTask;
        }

        public Task AddProjectAsync(ProjectInfo project, string hexPrivateKey)
        {
            var content = Newtonsoft.Json.JsonConvert.SerializeObject(project);

            var ev = new NostrEvent
            {
                Kind = NostrKind.Metadata,
                CreatedAt = DateTime.UtcNow,
                Content = content,
                Pubkey = project.NostrPubKey,
                Tags = new NostrEventTags(new NostrEventTag("ProjectDeclaration")) //TODO need to find the correct tags for the event
            };
            
            var key = NostrPrivateKey.FromHex(hexPrivateKey);
            var signed = ev.Sign(key);

            _nostrClient.Send(new NostrEventRequest(signed));

            _storage.StoreProjectInfo(project);
            
            return Task.CompletedTask;
        }

        public Task<ProjectInfo?> GetProjectAsync(string projectId)
        {
            var projectInfo = _storage.GetProjectById(projectId);

            return Task.FromResult(projectInfo);
        }

    }

}
