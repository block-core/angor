using System.Reactive.Linq;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;

namespace Angor.Client.Services
{
    public interface ISignService
    {
        DateTime RequestInvestmentSigs(SignRecoveryRequest signRecoveryRequest);
        void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey, DateTime sigRequestSentTime, Func<string, Task> action);

        Task LookupInvestmentRequestsAsync(string nostrPubKey, DateTime? since, Action<string, string, DateTime> action,
            Action onAllMessagesReceived);

        DateTime SendSignaturesToInvestor(string encryptedSignatureInfo, string nostrPrivateKey,
            string investorNostrPubKey);
    }

    public class SignService : ISignService
    {
        private readonly INostrCommunicationFactory _communicationFactory;
        private readonly INetworkService _networkService;
        
        private Dictionary<string,IDisposable> subscriptions = new ();
        private Dictionary<string, Action> eoseActions = new();
        public SignService(ILogger<NostrWebsocketClient> _logger, HttpClient httpClient, INostrCommunicationFactory communicationFactory, INetworkService networkService)
        {
            _communicationFactory = communicationFactory;
            _networkService = networkService;
            
            var nostrClient = _communicationFactory.CreateClient(_networkService);

            nostrClient.Streams.EoseStream.Subscribe(_ =>
            {
                _logger.LogInformation("End of stream on subscription" + _.Subscription);

                if (eoseActions.ContainsKey(_.Subscription))
                {
                    _logger.LogInformation("Invoking end of stream event on subscription" + _.Subscription);
                    eoseActions[_.Subscription].Invoke();
                    eoseActions.Remove(_.Subscription);
                }

                if (subscriptions.ContainsKey(_.Subscription))
                {
                    _logger.LogInformation("Closing and disposing of subscription - " + _.Subscription);
                    nostrClient.Send(new NostrCloseRequest(_.Subscription));
                    subscriptions[_.Subscription].Dispose();
                    subscriptions.Remove(_.Subscription);
                }
            });
        }

        public DateTime RequestInvestmentSigs(SignRecoveryRequest signRecoveryRequest)
        {
            var sender = NostrPrivateKey.FromHex(signRecoveryRequest.InvestorNostrPrivateKey);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = signRecoveryRequest.EncryptedContent,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(signRecoveryRequest.NostrPubKey), 
                    new NostrEventTag(NostrEventTag.CoordinatesIdentifier, NostrCoordinatesIdentifierTag(signRecoveryRequest.NostrPubKey)))
            };

            // Blazor does not support AES so needs to be done manually in the UI
            // var encrypted = ev.EncryptDirect(sender, receiver); 
            // var signed = encrypted.Sign(sender);

            var signed = ev.Sign(sender);

            var nostrClient = _communicationFactory.CreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return signed.CreatedAt!.Value;
        }

        public void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey, DateTime sigRequestSentTime, Func<string, Task> action)
        {
            var nostrClient = _communicationFactory.CreateClient(_networkService);
            
            var subscription = nostrClient.Streams.EventStream
                .Where(_ => _.Subscription == projectNostrPubKey)
                .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                .Subscribe(_ =>
                {
                    action.Invoke(_.Event.Content);
                });
            
            subscriptions.TryAdd(projectNostrPubKey,subscription);
            
            nostrClient.Send(new NostrRequest(projectNostrPubKey, new NostrFilter
            {
                Authors = new[] { projectNostrPubKey }, //From founder
                P = new[] { investorNostrPubKey }, // To investor
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = sigRequestSentTime,
                A = new[] { NostrCoordinatesIdentifierTag(projectNostrPubKey) }, //Only signature requests
                Limit = 1
            }));
        }

        public Task LookupInvestmentRequestsAsync(string nostrPubKey, DateTime? since, Action<string,string,DateTime> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.CreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "sig_req";
            
            var subscription = nostrClient.Streams.EventStream
                .Where(_ => _.Subscription == subscriptionKey)
                .Select(_ => _.Event)
                .Subscribe(_ =>
                {
                    action.Invoke(_.Pubkey,_.Content, _.CreatedAt.Value);
                });

            subscriptions.TryAdd(subscriptionKey, subscription);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                P = new[] { nostrPubKey },
                Kinds = new[] { NostrKind.EncryptedDm },
                A = new []{ NostrCoordinatesIdentifierTag(nostrPubKey)},
                Since = since
            }));
            
            eoseActions.TryAdd(subscriptionKey,onAllMessagesReceived);

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

            var nostrClient = _communicationFactory.CreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return ev.CreatedAt.Value;
        }

        private string NostrCoordinatesIdentifierTag(string nostrPubKey)
        {
            return $"{(int)NostrKind.ApplicationSpecificData}:{nostrPubKey}:AngorApp";
        }
    }
}
