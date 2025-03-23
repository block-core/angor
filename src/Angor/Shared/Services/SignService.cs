using System.Reactive.Linq;
using Angor.Shared.Models;
using Angor.Shared.Utilities;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;

namespace Angor.Shared.Services
{
    public class SignService :  ISignService
    {
        private readonly INostrCommunicationFactory _communicationFactory;
        private readonly INetworkService _networkService;
        private IRelaySubscriptionsHandling _subscriptionsHanding;
        private INostrNip59Actions _nip59Actions;
        
        private NostrKind _nup17Kind = (NostrKind)1059;
        private NostrKind _privateMessageKind = (NostrKind)14;

        public SignService(INostrCommunicationFactory communicationFactory, INetworkService networkService, IRelaySubscriptionsHandling subscriptionsHanding, INostrNip59Actions nip59Actions)
        {
            _communicationFactory = communicationFactory;
            _networkService = networkService;
            _subscriptionsHanding = subscriptionsHanding;
            _nip59Actions = nip59Actions;
        }

        public async Task<(DateTime, string)> RequestInvestmentSigsAsync(string content, string investorNostrPrivateKey,
            string founderNostrPubKey)
        {
            var privateKey = NostrPrivateKey.FromHex(investorNostrPrivateKey);
            
            var rumor = new NostrEvent
            {
                Kind = _privateMessageKind,
                CreatedAt = DateTime.UtcNow,
                Content = content,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(founderNostrPubKey),
                    new NostrEventTag("subject","Investment offer"))
            };
            
            // Blazor does not support AES so it needs to be done manually in javascript
            // var encrypted = ev.EncryptDirect(sender, receiver); 
            // var signed = encrypted.Sign(sender);
            
            var sentEvent = await SendNostrEventAsync(rumor,privateKey,founderNostrPubKey);

            return (sentEvent.CreatedAt!.Value, sentEvent.Id);
        }

        public void LookupSignatureForInvestmentRequest(string investorNsec, string projectNostrPubKey, DateTime? sigRequestSentTime, string sigRequestEventId, Func<string, Task> action)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            var key = NostrPrivateKey.FromHex(investorNsec);
            var investorNostrPubKey = key.DerivePublicKey().Hex; 
            
            if (!_subscriptionsHanding.RelaySubscriptionAdded(projectNostrPubKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == projectNostrPubKey)
                    .DistinctUntilChanged(x => x.Event?.Id)
                    .Where(x => x.Subscription == projectNostrPubKey)
                    .Where(x => x.Event?.Kind == _nup17Kind)
                    .SelectMany(async x => {
                        try
                        {
                            return await _nip59Actions.UnwrapEventAsync(x.Event!, investorNsec);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            throw;
                        }
                    })
                    .Where(_ => _?.Kind == _privateMessageKind)
                    .Where(_ => _.Tags.FindFirstTagValue("subject") == "Re:Investment offer")
                    .Subscribe(_ => { action.Invoke(_.Content); });

                _subscriptionsHanding.TryAddRelaySubscription(projectNostrPubKey, subscription);

            }

            nostrClient.Send(new NostrRequest(projectNostrPubKey, new NostrFilter
            {
              //  Authors = new[] { projectNostrPubKey }, //From founder
                P = new[] { investorNostrPubKey }, // To investor
                Kinds = new[] { _nup17Kind },
                Since = sigRequestSentTime,
                E = new [] { sigRequestEventId },
                Limit = 1,
            }));
        }

        public Task LookupInvestmentRequestsAsync(string projectNsec, string? senderNpub, DateTime? since,
            Action<string, string, string, DateTime> action, Action onAllMessagesReceived)
        {
            var projectNpub = NostrPrivateKey.FromHex(projectNsec).DerivePublicKey().Hex;
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = projectNpub + "sig_req";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .DistinctUntilChanged(x => x.Event?.Id)
                    .Where(x => x.Event?.Kind == _nup17Kind)
                    .Subscribe(async x => {
                        try {
                            // Process the event synchronously within the subscribe block
                            var unwrappedEvent = await _nip59Actions.UnwrapEventAsync(x.Event!, projectNsec);
                            if (unwrappedEvent != null && 
                                unwrappedEvent.Kind == _privateMessageKind && 
                                unwrappedEvent.Tags.FindFirstTagValue("subject") == "Investment offer")
                            {
                                action.Invoke(
                                    unwrappedEvent.Id, 
                                    unwrappedEvent.Pubkey, 
                                    unwrappedEvent.Content, 
                                    unwrappedEvent.CreatedAt.Value
                                );
                            }
                        }
                        catch (Exception ex) {
                            // Log the exception but don't rethrow to avoid breaking the subscription
                            Console.WriteLine($"Error processing event: {ex.Message}");
                        }
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            var nostrFilter = new NostrFilter
            {
                P = [projectNpub], //To founder,
                Kinds = [_nup17Kind],
                Since = since
            };

            if (senderNpub != null)  nostrFilter.Authors = [senderNpub]; //From investor

            nostrClient.Send(new NostrRequest(subscriptionKey, nostrFilter));

            return Task.CompletedTask;
        }

        public void LookupInvestmentRequestApprovals(string investorNsec, Action<string, DateTime, string> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var nostrPubKey = NostrPrivateKey.FromHex(investorNsec).DerivePublicKey().Hex;
            var subscriptionKey = nostrPubKey + "sig_res";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .DistinctUntilChanged(x => x.Event?.Id)
                    .Where(x => x.Event?.Kind == _nup17Kind)
                    .SelectMany(async x => {
                        var unwrappedEvent = await _nip59Actions.UnwrapEventAsync(x.Event!, investorNsec);
                        return new { Response = x, UnwrappedEvent = unwrappedEvent };
                    })
                    .Select(x => x.UnwrappedEvent)
                    .Where(_ => _?.Kind == _privateMessageKind)
                    .Where(_ => _.Tags.FindFirstTagValue("subject") == "Re:Investment offer")
                    .Subscribe(nostrEvent =>
                    {
                        action.Invoke(
                            nostrEvent.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier), 
                            nostrEvent.CreatedAt.Value, 
                            nostrEvent.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier));
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);
            
            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                //Authors = new[] { nostrPubKey }, //From founder TODO get the key used for the seal and filter by it
                P = new[] { nostrPubKey }, //To investor
                Kinds = new[] { _nup17Kind }
            }));
        }

        public async Task<DateTime> SendSignaturesToInvestorAsync(string signatureInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId)
        {
            var nostrPrivateKey = NostrPrivateKey.FromHex(nostrPrivateKeyHex);

            var rumor = new NostrEvent
            {
                Kind = _privateMessageKind,
                CreatedAt = DateTime.UtcNow,
                Content = signatureInfo,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(investorNostrPubKey),
                    NostrEventTag.Event(eventId),
                    new NostrEventTag("subject","Re:Investment offer")
                    )
            };
            
            var sentEvent = await SendNostrEventAsync(rumor,nostrPrivateKey, investorNostrPubKey);

            return sentEvent.CreatedAt.Value ;// rumor.CreatedAt.Value;
        }

        public async Task<DateTime> SendReleaseSigsToInvestorAsync(string releaseSigInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId)
        {
            var privateKey = NostrPrivateKey.FromHex(nostrPrivateKeyHex);

            var rumor = new NostrEvent
            {
                Kind = _privateMessageKind,
                CreatedAt = DateTime.UtcNow,
                Content = releaseSigInfo,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(investorNostrPubKey),
                    NostrEventTag.Event(eventId),
                    new NostrEventTag("subject", "Release transaction signatures"))
            };
            
            var sentEvent = await SendNostrEventAsync(rumor,privateKey, investorNostrPubKey);

            return sentEvent.CreatedAt.Value; //rumor.CreatedAt.Value;
        }

        public void LookupReleaseSigs(string investorNsec, string projectNostrPubKey, DateTime? releaseRequestSentTime, string releaseRequestEventId, Action<string> action, Action onAllMessagesReceived)
        {
            var key = NostrPrivateKey.FromHex(investorNsec);
            
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = projectNostrPubKey.Substring(0, 20) + "rel_sigs";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .DistinctUntilChanged(x => x.Event?.Id)
                    .Where(x => x.Event?.Kind == _nup17Kind)
                    .SelectMany(async x => {
                        var unwrappedEvent = await _nip59Actions.UnwrapEventAsync(x.Event!, investorNsec);
                        return new { Response = x, UnwrappedEvent = unwrappedEvent };
                    })
                    .Select(x => x.UnwrappedEvent)
                    .Where(_ => _?.Kind == _privateMessageKind)
                    .Where(_ => _.Tags.FindFirstTagValue("subject") == "Release transaction signatures")
                    .Subscribe(_ => { action.Invoke(_.Content); });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
               // Authors = new[] { projectNostrPubKey }, // From founder
                P = new[] { key.DerivePublicKey().Hex }, // To investor
                Kinds = new[] { _nup17Kind }, 
                Since = releaseRequestSentTime,
                E = new[] { releaseRequestEventId },
               // Limit = 1,
            }));
        }

        public void LookupSignedReleaseSigs(string nsec, Action<SignServiceLookupItem> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var key = NostrPrivateKey.FromHex(nsec);
            var subscriptionKey = string.Concat(key.DerivePublicKey().Hex.AsSpan(0, 20), "sing_sigs");

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .DistinctUntilChanged(x => x.Event?.Id)
                    .Where(x => x.Subscription == subscriptionKey)
                    .Where(x => x.Event?.Kind == _nup17Kind)
                    .SelectMany(async x => {
                            var unwrappedEvent = await _nip59Actions.UnwrapEventAsync(x.Event!, nsec);
                            return new { Response = x, UnwrappedEvent = unwrappedEvent };
                        })
                    .Select(x => x.UnwrappedEvent)
                    .Where(x => x != null)
                    .Where(x => x.Kind == _privateMessageKind)
                    .Where(x => x.Tags.FindFirstTagValue("subject") == "Release transaction signatures")
                    .Subscribe(nostrEvent =>
                    {
                        action.Invoke(new SignServiceLookupItem
                        {
                            NostrEvent = nostrEvent,
                            ProfileIdentifier = nostrEvent.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier),
                            EventCreatedAt = nostrEvent.CreatedAt.Value,
                            EventIdentifier = nostrEvent.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier)
                        });
                    });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
             //   Authors = new[] { key.DerivePublicKey().Hex }, // From founder
                Kinds = new[] { _nup17Kind },
                //Subject =  "Release transaction signatures"
            }));
        }

        
        private async Task<NostrEvent> SendNostrEventAsync(NostrEvent rumor, NostrPrivateKey privateKey, string recipientNpub)
        {
            var validEvent = rumor.DeepCloneWithPubKey(privateKey.DerivePublicKey().Hex);
            
            var sealedEvent = await _nip59Actions.SealEvent(validEvent, privateKey, recipientNpub);
            var wrappedEvent = await _nip59Actions.WrapEventAsync(sealedEvent, recipientNpub);
            
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(wrappedEvent));
            return wrappedEvent;
        }

        public void CloseConnection()
        {
            _subscriptionsHanding.Dispose();
        }
    }
    
    // public class NostrFilterWithSubject : NostrFilter
    // {
    //     /// <summary>A list of subjects to filter by, corresponding to the "subject" tag</summary>
    //     [JsonProperty("#subject")]
    //     public string? Subject { get; set; }
    // }
}
