﻿﻿using System.Reactive.Linq;
using Angor.Shared.Models;
using Newtonsoft.Json;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Shared.Services
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

        public (DateTime,string) RequestInvestmentSigs(string encryptedContent, string investorNostrPrivateKey, string founderNostrPubKey, Action<NostrOkResponse> okResponse)
        {
            var sender = NostrPrivateKey.FromHex(investorNostrPrivateKey);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedContent,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(founderNostrPubKey),
                    new NostrEventTag("subject","Investment offer"))
            };

            // Blazor does not support AES so it needs to be done manually in javascript
            // var encrypted = ev.EncryptDirect(sender, receiver); 
            // var signed = encrypted.Sign(sender);

            var signed = ev.Sign(sender);

            if(!_subscriptionsHanding.TryAddOKAction(signed.Id!,okResponse))
                throw new Exception("Failed to add OK action");
            
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return (signed.CreatedAt!.Value, signed.Id!);
        }

        public (DateTime,string) NotifyInvestmentCompleted(string encryptedContent, string investorNostrPrivateKey, string founderNostrPubKey, Action<NostrOkResponse> okResponse)
        {
            var sender = NostrPrivateKey.FromHex(investorNostrPrivateKey);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedContent,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(founderNostrPubKey),
                    new NostrEventTag("subject","Investment completed"))
            };

            var signed = ev.Sign(sender);

            if(!_subscriptionsHanding.TryAddOKAction(signed.Id!,okResponse))
                throw new Exception("Failed to add OK action");
            
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return (signed.CreatedAt!.Value, signed.Id!);
        }

        public void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey,
            DateTime? sigRequestSentTime, string sigRequestEventId, Func<string, Task> action,
            Action? onAllMessagesReceived = null)
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

            if (onAllMessagesReceived != null)
                _subscriptionsHanding.TryAddEoseAction(projectNostrPubKey, onAllMessagesReceived);

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

        public Task LookupInvestmentNotificationsAsync(string nostrPubKey, string? senderNpub, DateTime? since,
            Action<string, string, string, DateTime> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "inv_notif";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Investment completed")
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

        public (DateTime,string) NotifyInvestmentCanceled(string content, string investorNostrPrivateKey, string founderNostrPubKey, Action<NostrOkResponse> okResponse)
        {
            var sender = NostrPrivateKey.FromHex(investorNostrPrivateKey);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = content,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(founderNostrPubKey),
                    new NostrEventTag("subject","Investment canceled"))
            };

            var signed = ev.Sign(sender);

            if(!_subscriptionsHanding.TryAddOKAction(signed.Id!,okResponse))
                throw new Exception("Failed to add OK action");
            
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return (signed.CreatedAt!.Value, signed.Id!);
        }

        public Task LookupInvestmentCancellationsAsync(string nostrPubKey, string? senderNpub, DateTime? since,
            Action<string, string, string, DateTime> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "inv_cancel";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Investment canceled")
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
                P = [nostrPubKey],
                Kinds = [NostrKind.EncryptedDm],
                Since = since
            };

            if (senderNpub != null) nostrFilter.Authors = [senderNpub];

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

        public Task LookupAllInvestmentMessagesAsync(
            string nostrPubKey, 
            string? senderNpub, 
            DateTime? since,
            Action<InvestmentMessageType, string, string, string, DateTime> onMessage,
            Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var incomingKey = nostrPubKey.Substring(0, 20) + "_in";
            var outgoingKey = nostrPubKey.Substring(0, 20) + "_out";
            
            var receivedEoseCount = 0;
            
            void CheckAllReceived()
            {
                if (Interlocked.Increment(ref receivedEoseCount) >= 2)
                {
                    onAllMessagesReceived();
                }
            }

            void HandleEvent(NostrEvent nostrEvent)
            {
                var subject = nostrEvent.Tags.FindFirstTagValue("subject");
                var messageType = subject switch
                {
                    "Investment offer" => InvestmentMessageType.Request,
                    "Investment completed" => InvestmentMessageType.Notification,
                    "Investment canceled" => InvestmentMessageType.Cancellation,
                    "Re:Investment offer" => InvestmentMessageType.Approval,
                    _ => (InvestmentMessageType?)null
                };

                if (messageType.HasValue)
                {
                    // For approvals, we pass the referenced event ID (from e tag) as the id
                    // This allows matching the approval to the original request
                    var eventId = messageType.Value == InvestmentMessageType.Approval
                        ? nostrEvent.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier) ?? nostrEvent.Id
                        : nostrEvent.Id;
                    
                    onMessage.Invoke(
                        messageType.Value,
                        eventId,
                        nostrEvent.Pubkey,
                        nostrEvent.Content,
                        nostrEvent.CreatedAt!.Value);
                }
            }

            // Incoming messages subscription (requests, notifications, cancellations TO founder)
            if (!_subscriptionsHanding.RelaySubscriptionAdded(incomingKey))
            {
                var incomingSub = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == incomingKey)
                    .Select(_ => _.Event)
                    .Subscribe(HandleEvent);

                _subscriptionsHanding.TryAddRelaySubscription(incomingKey, incomingSub);
            }
            _subscriptionsHanding.TryAddEoseAction(incomingKey, CheckAllReceived);

            // Outgoing messages subscription (approvals FROM founder)
            if (!_subscriptionsHanding.RelaySubscriptionAdded(outgoingKey))
            {
                var outgoingSub = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == outgoingKey)
                    .Select(_ => _.Event)
                    .Subscribe(HandleEvent);

                _subscriptionsHanding.TryAddRelaySubscription(outgoingKey, outgoingSub);
            }
            _subscriptionsHanding.TryAddEoseAction(outgoingKey, CheckAllReceived);

            // Fetch messages TO founder (requests, notifications, cancellations)
            var incomingFilter = new NostrFilter
            {
                P = [nostrPubKey],
                Kinds = [NostrKind.EncryptedDm],
                Since = since
            };
            if (senderNpub != null) incomingFilter.Authors = [senderNpub];
            nostrClient.Send(new NostrRequest(incomingKey, incomingFilter));

            // Fetch messages FROM founder (approvals)
            var outgoingFilter = new NostrFilter
            {
                Authors = [nostrPubKey],
                Kinds = [NostrKind.EncryptedDm],
                Since = since
            };
            nostrClient.Send(new NostrRequest(outgoingKey, outgoingFilter));

            return Task.CompletedTask;
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

        public void LookupReleaseSigs(string investorNostrPubKey, string projectNostrPubKey, DateTime? releaseRequestSentTime, string releaseRequestEventId, Action<string> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = projectNostrPubKey.Substring(0, 20) + "rel_sigs";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Release transaction signatures")
                    .Subscribe(_ => { action.Invoke(_.Event.Content); });

                _subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            _subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

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

        public void LookupSignedReleaseSigs(string projectNostrPubKey, Action<SignServiceLookupItem> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = projectNostrPubKey.Substring(0, 20) + "sing_sigs";

            if (!_subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Release transaction signatures")
                    .Select(_ => _.Event)
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

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilterWithSubject
            {
                Authors = new[] { projectNostrPubKey }, // From founder
                Kinds = new[] { NostrKind.EncryptedDm },
                //Subject =  "Release transaction signatures"
            }));
        }

        public void CloseConnection()
        {
            _subscriptionsHanding.Dispose();
        }
    }
    
    public class NostrFilterWithSubject : NostrFilter
    {
        /// <summary>A list of subjects to filter by, corresponding to the "subject" tag</summary>
        [JsonProperty("#subject")]
        public string? Subject { get; set; }
    }
}
