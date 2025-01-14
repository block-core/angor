using System.Reactive.Linq;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;

namespace Angor.Client.Services
{
    public class SignService :  ISignService
    {
        private readonly INostrCommunicationFactory _communicationFactory;
        private readonly INetworkService _networkService;
        private IRelaySubscriptionsHandling _subscriptionsHanding;

        public SignService(INostrCommunicationFactory communicationFactory, INetworkService networkService, IRelaySubscriptionsHandling subscriptionsHanding)
        {
            _communicationFactory = communicationFactory;
            _networkService = networkService;
            _subscriptionsHanding = subscriptionsHanding;
        }

        public (DateTime,string) RequestInvestmentSigs(SignRecoveryRequest signRecoveryRequest)
        {
            var sender = NostrPrivateKey.FromHex(signRecoveryRequest.InvestorNostrPrivateKey);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = signRecoveryRequest.EncryptedContent,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(signRecoveryRequest.NostrPubKey),
                    new NostrEventTag("subject","Investment offer"))
            };

            // Blazor does not support AES so it needs to be done manually in javascript
            // var encrypted = ev.EncryptDirect(sender, receiver); 
            // var signed = encrypted.Sign(sender);

            var signed = ev.Sign(sender);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return (signed.CreatedAt!.Value, signed.Id);
        }

        public void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey, DateTime sigRequestSentTime, string sigRequestEventId, Func<string, Task> action)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            if (!_subscriptionsHanding.RelaySubscriptionAdded(projectNostrPubKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == projectNostrPubKey)
                    .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Re:Investment offer")
                    .Subscribe(_ => { action.Invoke(_.Event.Content); });

                _subscriptionsHanding.TryAddRelaySubscription(projectNostrPubKey, subscription);

            }

            nostrClient.Send(new NostrRequest(projectNostrPubKey, new NostrFilter
            {
                Authors = new[] { projectNostrPubKey }, //From founder
                P = new[] { investorNostrPubKey }, // To investor
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = sigRequestSentTime,
                E = new [] { sigRequestEventId },
                Limit = 1,
            }));
        }

        public Task LookupInvestmentRequestsAsync(string nostrPubKey, string? senderNpub, DateTime? since,
            Action<string, string, string, DateTime> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "sig_req";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Investment offer")
                    .Select(_ => _.Event)
                    .Subscribe(nostrEvent =>
                    {
                        action.Invoke(nostrEvent.Id, nostrEvent.Pubkey, nostrEvent.Content, nostrEvent.CreatedAt.Value);
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            var nostrFilter = new NostrFilter
            {
                P = [nostrPubKey], //To founder,
                Kinds = [NostrKind.EncryptedDm],
                Since = since
            };

            if (senderNpub != null)  nostrFilter.Authors = [senderNpub]; //From investor

            nostrClient.Send(new NostrRequest(subscriptionKey, nostrFilter));

            return Task.CompletedTask;
        }

        public void LookupInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime, string> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "sig_res";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Re:Investment offer")
                    .Select(_ => _.Event)
                    .Subscribe(nostrEvent =>
                    {
                        action.Invoke(nostrEvent.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier), nostrEvent.CreatedAt.Value, nostrEvent.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier));
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);
            
            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = new[] { nostrPubKey }, //From founder
                Kinds = new[] { NostrKind.EncryptedDm }
            }));
        }

        public DateTime SendSignaturesToInvestor(string encryptedSignatureInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId)
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
                    NostrEventTag.Event(eventId),
                    new NostrEventTag("subject","Re:Investment offer"), 
                })
            };

            var signed = ev.Sign(nostrPrivateKey);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return ev.CreatedAt.Value;
        }

        public DateTime SendReleaseSigsToInvestor(string encryptedReleaseSigInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId)
        {
            var nostrPrivateKey = NostrPrivateKey.FromHex(nostrPrivateKeyHex);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedReleaseSigInfo,
                Tags = new NostrEventTags(new[]
                {
                    NostrEventTag.Profile(investorNostrPubKey),
                    NostrEventTag.Event(eventId),
                    new NostrEventTag("subject", "Release transaction signatures"),
                })
            };

            var signed = ev.Sign(nostrPrivateKey);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return ev.CreatedAt.Value;
        }

        public void LookupReleaseSigs(string investorNostrPubKey, string projectNostrPubKey, DateTime releaseRequestSentTime, string releaseRequestEventId, Func<string, Task> action)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = projectNostrPubKey + "release_sigs";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Release transaction signatures")
                    .Subscribe(_ => { action.Invoke(_.Event.Content); });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = new[] { projectNostrPubKey }, // From founder
                P = new[] { investorNostrPubKey }, // To investor
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = releaseRequestSentTime,
                E = new[] { releaseRequestEventId },
                Limit = 1,
            }));
        }

        public void CloseConnection()
        {
            _subscriptionsHanding.Dispose();
        }
    }
}
