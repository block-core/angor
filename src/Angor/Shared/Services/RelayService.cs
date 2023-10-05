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
        Task AddProjectAsync(ProjectInfo project, string nsec);
        Task<ProjectInfo?> GetProjectAsync(string projectId);
    }

    public class RelayService : IRelayService
    {
        
        private readonly INostrClient _nostrClient;
      //  private const string nsec = "nsec1l0a7m5dlg4h9wurhnmgsq5nv9cqyvdwsutk4yf3w4fzzaqw7n80ssdfzkg";
        private readonly string _baseUrl = "/api/Test"; // "https://your-base-url/api/test";

        private readonly ISessionStorage _storage;
        private ILogger<RelayService> _logger;

        public RelayService(INostrClient httpClient, ISessionStorage storage, ILogger<RelayService> logger)
        {
            _nostrClient = httpClient;
            _storage = storage;
            _logger = logger;
        }

        public async Task RequestProjectDataAsync(string nostrPubKey)
        {

            _nostrClient.Send(new NostrFilter
                { Authors = new[] { nostrPubKey }, Kinds = new[] { NostrKind.Metadata } });
            
            var url = new Uri("wss://relay.damus.io");

            using var communicator = new NostrWebsocketCommunicator(url);

            _nostrClient.Streams.EventStream.Subscribe(response =>
            {
                var ev = response.Event;
                _logger.LogInformation("{kind}: {content}", ev?.Kind, ev?.Content);

                if (ev is not NostrMetadataEvent evm)
                    return;

                var projectInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectInfo>(ev.Content);
                _storage.StoreProjectInfo(projectInfo);
                _logger.LogInformation("Name: {name}, about: {about}", evm.Metadata?.Name, evm.Metadata?.About
            });

            await communicator.Start();
        }
        
        public Task AddProjectAsync(ProjectInfo project, string nsec)
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

            var key = NostrPrivateKey.FromBech32(nsec);
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
