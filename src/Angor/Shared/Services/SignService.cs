using System.Net.Http.Json;
using System.Reactive.Linq;
using Angor.Shared.Models;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;

namespace Angor.Client.Services
{
    public interface ISignService
    {
        Task AddSignKeyAsync(ProjectInfo project, string founderRecoveryPrivateKey, string nostrPrivateKey);
        Task<string> RequestInvestmentSigsAsync(SignRecoveryRequest signRecoveryRequest, Func<string,Task> action);
    }

    public class SignService : ISignService
    {

        private HttpClient _httpClient;
        private static INostrClient _nostrClient;
        private static INostrCommunicator _nostrCommunicator;

        private List<IDisposable> subscriptions = new ();
        public SignService(ILogger<NostrWebsocketClient> _logger, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _nostrCommunicator = new NostrWebsocketCommunicator(new Uri("wss://relay.angor.io"));

            _nostrCommunicator.Name = "angor-relay.test";
            _nostrCommunicator.ReconnectTimeout = null;
            
            _nostrCommunicator.DisconnectionHappened.Subscribe(info =>
            {
                _logger.LogError(info.Exception, "Relay disconnected, type: {type}, reason: {reason}.", info.Type, info.CloseStatus);
                _nostrCommunicator.Start();
            });
            _nostrCommunicator.MessageReceived.Subscribe(info => _logger.LogInformation(info.Text, "Relay message received, type: {type}", info.MessageType));
            
            _nostrCommunicator.StartOrFail();
            
            _nostrClient = new NostrWebsocketClient(_nostrCommunicator, _logger);
        }

        public async Task AddSignKeyAsync(ProjectInfo project, string founderRecoveryPrivateKey, string nostrPrivateKey)
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/TestSign",
                new SignData
                {
                    ProjectIdentifier = project.ProjectIdentifier, 
                    FounderRecoveryPrivateKey = founderRecoveryPrivateKey,
                    NostrPrivateKey = nostrPrivateKey
                });
             response.EnsureSuccessStatusCode();
        }

        public Task<string> RequestInvestmentSigsAsync(SignRecoveryRequest signRecoveryRequest, Func<string,Task> action)
        {
            var sender = NostrPrivateKey.FromHex(signRecoveryRequest.InvestorNostrPrivateKey);
            //var receiver = NostrPublicKey.FromHex(signRecoveryRequest.NostrPubKey);
            
            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = signRecoveryRequest.content,
                Tags = new NostrEventTags(new []{NostrEventTag.Profile(signRecoveryRequest.NostrPubKey)})
            };

            // Blazor does not support AES so needs to be done manually in the UI
            // var encrypted = ev.EncryptDirect(sender, receiver); 
            // var signed = encrypted.Sign(sender);

            var signed = ev.Sign(sender);
            var timeOfMessage = DateTime.UtcNow;

            _nostrClient.Send(new NostrEventRequest(signed));

            var nostrPubKey = sender.DerivePublicKey().Hex;
            
            _nostrClient.Send(new NostrRequest(nostrPubKey, new NostrFilter
            {
                Authors = new []{signRecoveryRequest.NostrPubKey},
                P = new []{nostrPubKey},
                Kinds = new[] { NostrKind.EncryptedDm},
                Since = timeOfMessage,
                Limit = 1
            }));

            var subscription = _nostrClient.Streams.EventStream
                .Where(_ => _.Subscription == nostrPubKey)
                .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                .Subscribe(_ =>
                {
                    action.Invoke(_.Event.Content);
                });
            
            subscriptions.Add(subscription); //TODO dispose of if after the signatures have been received
            
            return Task.FromResult(signed.Id!);
        }
    }
}
