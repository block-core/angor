using System.Reactive.Linq;
using System.Text.Json;
using Angor.Shared.Models;
using Microsoft.Extensions.Logging;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;

namespace Angor.Shared.Services
{
    public class SignService : ISignService
    {
        private ILogger<SignService> _logger;

        private readonly INostrCommunicationFactory _communicationFactory;
        private readonly INetworkService _networkService;
        private IRelaySubscriptionsHandling _subscriptionsHanding;

        public SignService(ILogger<SignService> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService,
            IRelaySubscriptionsHandling subscriptionsHanding)
        {
            _communicationFactory = communicationFactory;
            _networkService = networkService;
            _subscriptionsHanding = subscriptionsHanding;
            _logger = logger;
        }

        public (DateTime, string) RequestInvestmentSigs(SignRecoveryRequest signRecoveryRequest)
        {
            var sender = NostrPrivateKey.FromHex(signRecoveryRequest.InvestorNostrPrivateKey);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = signRecoveryRequest.EncryptedContent,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(signRecoveryRequest.NostrPubKey),
                    new NostrEventTag(NostrEventTag.CoordinatesIdentifier,
                        NostrCoordinatesIdentifierTag(signRecoveryRequest.NostrPubKey)))
            };

            // Blazor does not support AES so it needs to be done manually in javascript
            // var encrypted = ev.EncryptDirect(sender, receiver); 
            // var signed = encrypted.Sign(sender);

            var signed = ev.Sign(sender);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return (signed.CreatedAt!.Value, signed.Id);
        }

        public void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey,
            DateTime sigRequestSentTime, string sigRequestEventId, Func<string, Task> action)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            if (!_subscriptionsHanding.RelaySubscriptionAdded(projectNostrPubKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == projectNostrPubKey)
                    .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                    .Subscribe(_ => { action.Invoke(_.Event.Content); });

                _subscriptionsHanding.TryAddRelaySubscription(projectNostrPubKey, subscription);

            }

            nostrClient.Send(new NostrRequest(projectNostrPubKey, new NostrFilter
            {
                Authors = new[] { projectNostrPubKey }, //From founder
                P = new[] { investorNostrPubKey }, // To investor
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = sigRequestSentTime,
                E = new[] { sigRequestEventId },
                //A = new[] { NostrCoordinatesIdentifierTag(projectNostrPubKey) }, //Only signature requests
                Limit = 1,
            }));
        }

        public Task LookupInvestmentRequestsAsync(string nostrPubKey, DateTime? since,
            Action<string, string, string, DateTime> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "sig_req";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Select(_ => _.Event)
                    .Subscribe(nostrEvent =>
                    {
                        action.Invoke(nostrEvent.Id, nostrEvent.Pubkey, nostrEvent.Content,
                            nostrEvent.CreatedAt.Value);
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                P = new[] { nostrPubKey }, //To founder
                Kinds = new[] { NostrKind.EncryptedDm },
                A = new[] { NostrCoordinatesIdentifierTag(nostrPubKey) }, //Only signature requests
                Since = since
            }));

            return Task.CompletedTask;
        }

        public void LookupInvestmentRequestsSentByInvestorKey(string nostrPubKey, Action<string, string, DateTime,string> action,
            Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey[..(nostrPubKey.Length/2)] + "Find";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Select(_ => _.Event)
                    .Subscribe(nostrEvent =>
                    {
                        var tag = nostrEvent.Tags.FindFirstTagValue(NostrEventTag.CoordinatesIdentifier);
                        
                        if (TryGetNostrFounderPubKeyFromTag(tag, out var founderNostrPubKey) )
                        {
                            action.Invoke(nostrEvent.Pubkey, founderNostrPubKey, nostrEvent.CreatedAt.Value,nostrEvent.Id);
                        }
                        
                        _logger.LogDebug(JsonSerializer.Serialize(nostrEvent));
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }
            
            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = new[] { nostrPubKey }, // from investor
                Kinds = new[] { NostrKind.EncryptedDm }
            }));
        }

        public void LookupInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime, string> action,
            Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "sig_res";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Select(_ => _.Event)
                    .Subscribe(nostrEvent =>
                    {
                        action.Invoke(
                            nostrEvent.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier),
                            nostrEvent.CreatedAt!.Value,
                            nostrEvent.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier) ?? string.Empty);
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = new[] { nostrPubKey }, //From founder
                Kinds = new[] { NostrKind.EncryptedDm },
                A = new[] { NostrCoordinatesIdentifierTag(nostrPubKey) } //Only signature requests
            }));
        }

        public DateTime SendSignaturesToInvestor(string encryptedSignatureInfo, string nostrPrivateKeyHex,
            string investorNostrPubKey, string eventId)
        {
            var nostrPrivateKey = NostrPrivateKey.FromHex(nostrPrivateKeyHex);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedSignatureInfo,
                Tags = new NostrEventTags(new[]
                {
                    NostrEventTag.Profile(investorNostrPubKey),
                    new NostrEventTag(NostrEventTag.CoordinatesIdentifier,
                        NostrCoordinatesIdentifierTag(nostrPrivateKey.DerivePublicKey().Hex)),
                    NostrEventTag.Event(eventId)
                })
            };

            var signed = ev.Sign(nostrPrivateKey);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return ev.CreatedAt.Value;
        }

        public void CloseConnection()
        {
            _subscriptionsHanding.Dispose();
        }

        private string NostrCoordinatesIdentifierTag(string nostrPubKey)
        {
            return $"{(int)NostrKind.ApplicationSpecificData}:{nostrPubKey}:AngorApp";
        }

        private bool TryGetNostrFounderPubKeyFromTag(string? tagValue, out string? founderNostrPubKey)
        {
            founderNostrPubKey = null;
            
            if (tagValue is null)
                return false;
            
            var arr = tagValue.Split(":");

            if (arr.Length != 3 || 
                arr[0] != $"{(int)NostrKind.ApplicationSpecificData}" ||
                arr[2] != "AngorApp") 
                return false;
            
            founderNostrPubKey = arr[1];
            return true;
        }
    }
    
}
