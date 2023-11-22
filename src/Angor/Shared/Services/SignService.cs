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

        Task LookupInvestmentRequestsAsync(string nostrPubKey, DateTime? since, Action<string, string, DateTime> action,
            Action onAllMessagesReceived);

        DateTime SendSignaturesToInvestor(string encryptedSignatureInfo, string nostrPrivateKey,
            string investorNostrPubKey);
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
                Content = signRecoveryRequest.EncryptedContent,
                Tags = new NostrEventTags(new[]
                {
                    NostrEventTag.Profile(signRecoveryRequest.NostrPubKey),
                    new NostrEventTag(NostrEventTag.CoordinatesIdentifier,
                        NostrCoordinatesIdentifierTag(signRecoveryRequest.NostrPubKey))
                })
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

        public Task LookupInvestmentRequestsAsync(string nostrPubKey, DateTime? since, Action<string,string,DateTime> action, Action onAllMessagesReceived)
        {
            var subscriptionKey = nostrPubKey + "sig_req";
            
            _nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                P = new[] { nostrPubKey },
                Kinds = new[] { NostrKind.EncryptedDm },
                A = new []{ NostrCoordinatesIdentifierTag(nostrPubKey)},
                Since = since
            }));

            var subscription = _nostrClient.Streams.EventStream
                .Where(_ => _.Subscription == subscriptionKey)
                //.Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                .Select(_ => _.Event)
                .Subscribe(_ =>
                {
                    action.Invoke(_.Pubkey,_.Content, _.CreatedAt.Value);
                });

            if (!subscriptions.Contains(subscription))
            {
                subscriptions.Add(subscription);
            }

            var todo =  _nostrClient.Streams.EoseStream
                .Where(_ => _.Subscription == subscriptionKey)
                .Subscribe(_ => onAllMessagesReceived.Invoke());

            return Task.CompletedTask;
        }

        public DateTime SendSignaturesToInvestor(string encryptedSignatureInfo, string nostrPrivateKeyHex, string investorNostrPubKey)
        {
            var nostrPrivateKey = NostrPrivateKey.FromHex(nostrPrivateKeyHex);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedSignatureInfo,
                Tags = new NostrEventTags(new []
                {
                    NostrEventTag.Profile(investorNostrPubKey),
                    new NostrEventTag(NostrEventTag.CoordinatesIdentifier,NostrCoordinatesIdentifierTag(nostrPrivateKey.DerivePublicKey().Hex))
                })
            };

            var signed = ev.Sign(nostrPrivateKey);

            _nostrClient.Send(new NostrEventRequest(signed));

            return ev.CreatedAt.Value;
        }

        private string NostrCoordinatesIdentifierTag(string nostrPubKey)
        {
            return $"{(int)NostrKind.ApplicationSpecificData}:{nostrPubKey}:AngorApp";
        }
    }
}
