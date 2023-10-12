using Angor.Client.Services;
using Angor.Shared.Models;
using Nostr.Client.Messages.Metadata;
using Nostr.Client.Requests;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Metadata;
using Nostr.Client.Requests;

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
        
        private INostrClient _nostrClient;
        private INostrCommunicator _nostrCommunicator;

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
            _nostrCommunicator = new NostrWebsocketCommunicator(new Uri("ws://angor-relay.test"));

            _nostrCommunicator.Name = "angor-relay.test";
            
            _nostrCommunicator.DisconnectionHappened.Subscribe(info => _logger.LogError(info.Exception,"[{relay}] Disconnected, type: {type}, reason: {reason}", "test", info.Type, info.CloseStatus));
            
            await _nostrCommunicator.StartOrFail();
            
            _nostrClient =
                new NostrWebsocketClient(_nostrCommunicator, _clientLogger);

            _nostrClient.Streams.UnknownMessageStream.Subscribe(_ =>
                _logger.LogError(_.ToString(), "unknown event"));
            
            _nostrClient.Send(new NostrFilter
                { Authors = new[] { "c62a0e7f62d990eca33b9a56799137d55de4b7fb65e5fcc307ec010c01dc1b5c" }, Kinds = new[] { NostrKind.Metadata } });
            
            
            // _nostrCommunicator.DisconnectionHappened.Subscribe(_ => _logger.LogError(_.Exception, "failed to connect"));
            //
            // await _nostrCommunicator.StartOrFail();
        //     using var communicator = new NostrWebsocketCommunicator(new Uri("wss://angor-relay-web.test"));
        //     
        //     using (  var client = new NostrWebsocketClient(communicator,_clientLogger))
        //     {
        //         client.Communicator.DisconnectionHappened.Subscribe(_ => _logger.LogError(_.Exception, "failed to connect"));
        //         
        //         client.Streams.EventStream.Subscribe(_ => _logger.LogInformation(_.Event.Content));
        //
        //         await client.Communicator.StartOrFail();
        //         
        //         client.Send(new NostrFilter
        //             { Authors = new[] { "c62a0e7f62d990eca33b9a56799137d55de4b7fb65e5fcc307ec010c01dc1b5c" }, Kinds = new[] { NostrKind.Metadata } });
        //     }
        //     
        //     
        //     var test =
        //         new NostrWebsocketClient(new NostrWebsocketCommunicator(new Uri("wss://localhost:3000")),_clientLogger);
        //     
        //     test.Communicator.DisconnectionHappened.Subscribe(_ => _logger.LogError(_.Exception, "failed to connect"));
        //     
        //     await test.Communicator.StartOrFail();
         }
        
        public Task RequestProjectDataAsync(string nostrPubKey)
        {
            _nostrClient.Send(new NostrFilter
                { Authors = new[] { nostrPubKey }, Kinds = new[] { NostrKind.Metadata } });

            _nostrClient.Streams.EventStream.Subscribe(response =>
            {
                var ev = response.Event;
                _logger.LogInformation("{kind}: {content}", ev?.Kind, ev?.Content);

                if (ev is not NostrMetadataEvent evm)
                    return;

                var projectInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectInfo>(ev.Content);
                _storage.StoreProjectInfo(projectInfo);
                _logger.LogInformation("Name: {name}, about: {about}", evm.Metadata?.Name, evm.Metadata?.About);
            });

            return Task.CompletedTask;
            //await _nostrCommunicator.Start();
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
                Tags = new NostrEventTags(new NostrEventTag("ProjectDeclaration")) 
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
