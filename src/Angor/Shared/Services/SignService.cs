using Angor.Shared.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;

namespace Angor.Client.Services
{
    public interface ISignService
    {
        Task AddSignKeyAsync(ProjectInfo project, string founderRecoveryPrivateKey);
        Task<SignatureInfo> GetInvestmentSigsAsync(SignRecoveryRequest signRecoveryRequest);
    }

    public class SignService : ISignService
    {

        private static INostrClient _nostrClient;
        private static INostrCommunicator _nostrCommunicator;

        public SignService(ILogger<NostrWebsocketClient> _logger)
        {
            _nostrCommunicator = new NostrWebsocketCommunicator(new Uri("ws://angor-relay.test"));

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

        public async Task AddSignKeyAsync(ProjectInfo project, string founderRecoveryPrivateKey)
        {
            // var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}", new SignData { ProjectIdentifier = project.ProjectIdentifier, FounderRecoveryPrivateKey = founderRecoveryPrivateKey });
            // response.EnsureSuccessStatusCode();
        }

        public Task<SignatureInfo> GetInvestmentSigsAsync(SignRecoveryRequest signRecoveryRequest)
        {
            var sender = NostrPrivateKey.FromHex(signRecoveryRequest.InvestorNostrPrivateKey);
            var receiver = NostrPublicKey.FromHex(signRecoveryRequest.NostrPubKey);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = JsonConvert.SerializeObject(new {text = "The transaction to be signed", signRecoveryRequest.ProjectIdentifier,signRecoveryRequest.InvestmentTransaction}),
                Tags = new NostrEventTags(new []{NostrEventTag.Profile(sender.DerivePublicKey().Hex)})
            };

            var encrypted = ev.EncryptDirect(sender, receiver);
            var signed = encrypted.Sign(sender);

            _nostrClient.Send(new NostrEventRequest(signed));

            return Task.FromResult(new SignatureInfo());
        }
    }
}
