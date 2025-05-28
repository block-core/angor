using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using Newtonsoft.Json;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Shared.Services
{
    public class SignService(
        ISensitiveNostrData sensitiveNostrData,
        ISerializer serializer,
        INostrEncryption nostrEncryption,
        INostrCommunicationFactory communicationFactory,
        INetworkService networkService,
        IRelaySubscriptionsHandling subscriptionsHanding,
        INostrService nostrService)
        : ISignService
    {
        public (DateTime, string) PostInvestmentRequest(string encryptedContent, string investorNostrPrivateKey,
            string founderNostrPubKey, Action<NostrOkResponse> okResponse)
        {
            var sender = NostrPrivateKey.FromHex(investorNostrPrivateKey);

            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedContent,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(founderNostrPubKey),
                    new NostrEventTag("subject", "Investment offer"))
            };

            // Blazor does not support AES so it needs to be done manually in javascript
            // var encrypted = ev.EncryptDirect(sender, founderNostrPubKey); 
            // var signed = encrypted.Sign(sender);

            var signed = ev.Sign(sender);

            if (!subscriptionsHanding.TryAddOKAction(signed.Id!, okResponse))
                throw new Exception("Failed to add OK action");

            var nostrClient = communicationFactory.GetOrCreateClient(networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return (signed.CreatedAt!.Value, signed.Id!);
        }

        public void GetInvestmentRequestApproval(string investorNostrPubKey, string projectNostrPubKey,
            DateTime? sigRequestSentTime, string sigRequestEventId, Func<string, Task> action)
        {
            var nostrClient = communicationFactory.GetOrCreateClient(networkService);

            if (!subscriptionsHanding.RelaySubscriptionAdded(projectNostrPubKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == projectNostrPubKey)
                    .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Re:Investment offer")
                    .Subscribe(_ => { action.Invoke(_.Event.Content); });

                subscriptionsHanding.TryAddRelaySubscription(projectNostrPubKey, subscription);
            }

            nostrClient.Send(new NostrRequest(projectNostrPubKey, new NostrFilter
            {
                Authors = new[] { projectNostrPubKey }, //From founder
                P = new[] { investorNostrPubKey }, // To investor
                Kinds = new[] { NostrKind.EncryptedDm },
                Since = sigRequestSentTime,
                E = new[] { sigRequestEventId },
                Limit = 1,
            }));
        }

        public Task GetAllInvestmentRequests(string nostrPubKey,
            string? senderNpub,
            DateTime? since,
            Action<string, string, string, DateTime> action,
            Action onAllMessagesReceived)
        {
            var nostrClient = communicationFactory.GetOrCreateClient(networkService);
            var subscriptionKey = nostrPubKey + "sig_req";

            if (!subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Investment offer")
                    .Select(_ => _.Event)
                    .Subscribe(nostrEvent =>
                    {
                        action.Invoke(nostrEvent.Id, nostrEvent.Pubkey, nostrEvent.Content,
                            nostrEvent.CreatedAt.Value);
                    });

                subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            var nostrFilter = new NostrFilter
            {
                P = [nostrPubKey], //To founder,
                Kinds = [NostrKind.EncryptedDm],
                Since = since
            };

            if (senderNpub != null) nostrFilter.Authors = [senderNpub]; //From investor

            nostrClient.Send(new NostrRequest(subscriptionKey, nostrFilter));

            return Task.CompletedTask;
        }

        public void GetAllInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime, string> action,
            Action onAllMessagesReceived)
        {
            var nostrClient = communicationFactory.GetOrCreateClient(networkService);
            var subscriptionKey = nostrPubKey + "sig_res";

            if (!subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Re:Investment offer")
                    .Select(_ => _.Event)
                    .Subscribe(nostrEvent =>
                    {
                        action.Invoke(nostrEvent.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier),
                            nostrEvent.CreatedAt.Value,
                            nostrEvent.Tags.FindFirstTagValue(NostrEventTag.EventIdentifier));
                    });

                subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = new[] { nostrPubKey }, //From founder
                Kinds = new[] { NostrKind.EncryptedDm }
            }));
        }

        public DateTime PostInvestmentRequestApproval(string encryptedSignatureInfo, string nostrPrivateKeyHex,
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
                    NostrEventTag.Event(eventId),
                    new NostrEventTag("subject", "Re:Investment offer"),
                })
            };

            var signed = ev.Sign(nostrPrivateKey);

            var nostrClient = communicationFactory.GetOrCreateClient(networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return ev.CreatedAt.Value;
        }

        public DateTime PostInvestmentRevocation(string encryptedReleaseSigInfo, string nostrPrivateKeyHex,
            string investorNostrPubKey, string eventId)
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

            var nostrClient = communicationFactory.GetOrCreateClient(networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return ev.CreatedAt.Value;
        }

        public void GetInvestmentRevocation(string investorNostrPubKey, string projectNostrPubKey,
            DateTime? releaseRequestSentTime, string releaseRequestEventId, Action<string> action,
            Action onAllMessagesReceived)
        {
            var nostrClient = communicationFactory.GetOrCreateClient(networkService);
            var subscriptionKey = projectNostrPubKey.Substring(0, 20) + "rel_sigs";

            if (!subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
            {
                var subscription = nostrClient.Streams.EventStream
                    .Where(_ => _.Subscription == subscriptionKey)
                    .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                    .Where(_ => _.Event.Tags.FindFirstTagValue("subject") == "Release transaction signatures")
                    .Subscribe(_ => { action.Invoke(_.Event.Content); });

                subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

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

        public void GetAllInvestmentRevocations(string projectNostrPubKey, Action<SignServiceLookupItem> action,
            Action onAllMessagesReceived)
        {
            var nostrClient = communicationFactory.GetOrCreateClient(networkService);
            var subscriptionKey = projectNostrPubKey.Substring(0, 20) + "sing_sigs";

            if (!subscriptionsHanding.RelaySubscriptionAdded(subscriptionKey))
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

                subscriptionsHanding.TryAddRelaySubscription(subscriptionKey, subscription);
            }

            subscriptionsHanding.TryAddEoseAction(subscriptionKey, onAllMessagesReceived);

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilterWithSubject
            {
                Authors = new[] { projectNostrPubKey }, // From founder
                Kinds = new[] { NostrKind.EncryptedDm },
                //Subject =  "Release transaction signatures"
            }));
        }

        public void CloseConnection()
        {
            subscriptionsHanding.Dispose();
        }

        public async Task<Result<EventSendResponse>> PostInvestmentRequest2<T>(KeyIdentifier keyIdentifier, T content,
            string founderNostrPubKey)
        {
            var key = await sensitiveNostrData.GetNostrPrivateKey(keyIdentifier);
            var parsedKey = NostrPrivateKey.FromHex(key.Value);
            var jsonContent = serializer.Serialize(content);
            var encryptedContent = await nostrEncryption.Nip44Encryption(jsonContent, key.Value, founderNostrPubKey);
            var ev = new NostrEvent
            {
                Kind = NostrKind.EncryptedDm,
                CreatedAt = DateTime.UtcNow,
                Content = encryptedContent,
                Tags = new NostrEventTags(
                    NostrEventTag.Profile(founderNostrPubKey),
                    new NostrEventTag("subject", "Investment offer"))
            };
            
            var signed = ev.Sign(parsedKey);

            return await nostrService.Send(signed)
                .Ensure(response => response.Accepted, "Failed to send event")
                .Map(response => new EventSendResponse(response.Accepted, response.EventId, response.Message,
                    response.ReceivedTimestamp));
        }

        public Task<Result<EventSendResponse>> PostInvestmentRequestApproval2<T>(KeyIdentifier keyIdentifier, T content,
            string investorNostrPubKey, string eventId)
        {
            return sensitiveNostrData.GetNostrPrivateKey(keyIdentifier)
                .Map(key => (Key:key, ParsedKey: NostrPrivateKey.FromHex(key), JsonContent: serializer.Serialize(content)))
                .Bind(async data =>
                {
                    var encryptedContent = await nostrEncryption.Nip44Encryption(data.JsonContent, data.ParsedKey.Hex,
                        investorNostrPubKey);
                    var parsedKey = data.ParsedKey;
                    return Result.Success((parsedKey, encryptedContent));
                })
                .Map(data =>
                {
                    var ev = new NostrEvent
                    {
                        Kind = NostrKind.EncryptedDm,
                        CreatedAt = DateTime.UtcNow,
                        Content = data.encryptedContent,
                        Tags = new NostrEventTags(
                            NostrEventTag.Profile(investorNostrPubKey),
                            NostrEventTag.Event(eventId),
                            new NostrEventTag("subject", "Re:Investment offer"))
                    };
                    return ev.Sign(data.parsedKey);
                })
                .Bind(data => nostrService.Send(data)
                    .Ensure(response => response.Accepted, "Failed to send event"))
                .Map(response => new EventSendResponse(response.Accepted, response.EventId, response.Message,
                    response.ReceivedTimestamp));
        }


        public class NostrFilterWithSubject : NostrFilter
        {
            /// <summary>A list of subjects to filter by, corresponding to the "subject" tag</summary>
            [JsonProperty("#subject")]
            public string? Subject { get; set; }
        }
    }
}