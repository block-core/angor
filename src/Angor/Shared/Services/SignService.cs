﻿using System.Reactive.Linq;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Requests;

namespace Angor.Client.Services
{
    public class SignService : RelaySubscriptionsHanding, ISignService
    {
        private readonly INostrCommunicationFactory _communicationFactory;
        private readonly INetworkService _networkService;
        
        private readonly List<IDisposable> _serviceSubscriptions;

        public SignService(ILogger<NostrWebsocketClient> _logger, ILogger<RelaySubscriptionsHanding> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService)
        : base(logger,communicationFactory,networkService)
        {
            _communicationFactory = communicationFactory;
            _networkService = networkService;
            
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            _serviceSubscriptions = new();
            _serviceSubscriptions.Add(nostrClient.Streams.EoseStream.Subscribe(HandleEoseMessages));
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

            // Blazor does not support AES so it needs to be done manually in javascript
            // var encrypted = ev.EncryptDirect(sender, receiver); 
            // var signed = encrypted.Sign(sender);

            var signed = ev.Sign(sender);

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return signed.CreatedAt!.Value;
        }

        public void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey, DateTime sigRequestSentTime, Func<string, Task> action)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            
            var subscription = nostrClient.Streams.EventStream
                .Where(_ => _.Subscription == projectNostrPubKey)
                .Where(_ => _.Event.Kind == NostrKind.EncryptedDm)
                .Subscribe(_ =>
                {
                    action.Invoke(_.Event.Content);
                });
            
            userSubscriptions.TryAdd(projectNostrPubKey, new SubscriptionCallCounter<IDisposable>(subscription));
            
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
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "sig_req";
            
            var subscription = nostrClient.Streams.EventStream
                .Where(_ => _.Subscription == subscriptionKey)
                .Select(_ => _.Event)
                .Subscribe(_ =>
                {
                    action.Invoke(_.Pubkey,_.Content, _.CreatedAt.Value);
                });

            userSubscriptions.TryAdd(subscriptionKey,  new SubscriptionCallCounter<IDisposable>(subscription));
            userEoseActions.TryAdd(subscriptionKey,new SubscriptionCallCounter<Action>(onAllMessagesReceived));

            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                P = new[] { nostrPubKey }, //To founder
                Kinds = new[] { NostrKind.EncryptedDm },
                A = new []{ NostrCoordinatesIdentifierTag(nostrPubKey)}, //Only signature requests
                Since = since
            }));

            return Task.CompletedTask;
        }

        public void LookupInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime> action, Action onAllMessagesReceived)
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            var subscriptionKey = nostrPubKey + "sig_res";
            
            var subscription = nostrClient.Streams.EventStream
                .Where(_ => _.Subscription == subscriptionKey)
                .Select(_ => _.Event)
                .Subscribe(_ =>
                {
                    action.Invoke(_.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier), _.CreatedAt.Value);
                });
            
            userSubscriptions.TryAdd(subscriptionKey,  new SubscriptionCallCounter<IDisposable>(subscription));
            userEoseActions.TryAdd(subscriptionKey,new SubscriptionCallCounter<Action>(onAllMessagesReceived));
            
            nostrClient.Send(new NostrRequest(subscriptionKey, new NostrFilter
            {
                Authors = new[] { nostrPubKey }, //From founder
                Kinds = new[] { NostrKind.EncryptedDm },
                A = new[] { NostrCoordinatesIdentifierTag(nostrPubKey) }, //Only signature requests
            }));
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

            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);
            nostrClient.Send(new NostrEventRequest(signed));

            return ev.CreatedAt.Value;
        }

        public void CloseConnection()
        {
            _serviceSubscriptions.ForEach(subscription => subscription.Dispose());
            Dispose();
        }

        private string NostrCoordinatesIdentifierTag(string nostrPubKey)
        {
            return $"{(int)NostrKind.ApplicationSpecificData}:{nostrPubKey}:AngorApp";
        }
    }
}
