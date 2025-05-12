using Angor.Client.Models;
using Angor.Shared.Services;
using Angor.Shared.Utilities;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Direct;
using Nostr.Client.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Nostr.Client.Messages.Contacts;

namespace Angor.Client.Services
{
    public class MessageService : IMessageService
    {
        private readonly ILogger<MessageService> _logger;
        private readonly INostrCommunicationFactory _nostrCommunicationFactory;
        private readonly IEncryptionService _encryptionService;
        private readonly INetworkService _networkService;
        private readonly IRelayService _relayService;
        private readonly NostrConversionHelper _nostrHelper;

        private Dictionary<string, DirectMessage> _directMessagesDictionary = new Dictionary<string, DirectMessage>();
        private IDisposable _messageSubscription;
        private string _currentUserPrivateKeyHex;
        private string _currentUserNpub;
        private string _currentUserHexPub;
        private string _contactHexPub;
        private bool _subscriptionActive = false;

        public IReadOnlyList<DirectMessage> DirectMessages => _directMessagesDictionary.Values
            .OrderBy(m => m.Timestamp)
            .ToList()
            .AsReadOnly();
        public bool IsLoadingMessages { get; private set; }
        public bool IsSendingMessage { get; private set; }
        public bool IsRefreshing { get; private set; }
        public bool IsSubscriptionActive => _subscriptionActive;

        public event Action OnChange;

        public MessageService(
            ILogger<MessageService> logger,
            INostrCommunicationFactory nostrCommunicationFactory,
            IEncryptionService encryptionService,
            INetworkService networkService,
            IRelayService relayService,
            NostrConversionHelper nostrHelper)
        {
            _logger = logger;
            _nostrCommunicationFactory = nostrCommunicationFactory;
            _encryptionService = encryptionService;
            _networkService = networkService;
            _relayService = relayService;
            _nostrHelper = nostrHelper;
        }

        public void SetKeys(string currentUserPrivateKeyHex, string currentUserNpub, string contactHexPub)
        {
            _currentUserPrivateKeyHex = currentUserPrivateKeyHex;
            _currentUserNpub = currentUserNpub;
            _contactHexPub = contactHexPub;

            if (!string.IsNullOrEmpty(_currentUserNpub))
            {
                _currentUserHexPub = _nostrHelper.ConvertBech32ToHex(_currentUserNpub);
            }
            else
            {
                _currentUserHexPub = null;
            }
        }

        public async Task InitializeAsync(string currentUserPrivateKeyHex, string currentUserNpub, string contactHexPub)
        {
            SetKeys(currentUserPrivateKeyHex, currentUserNpub, contactHexPub);

            if (string.IsNullOrEmpty(_currentUserPrivateKeyHex) || string.IsNullOrEmpty(_contactHexPub) || string.IsNullOrEmpty(_currentUserHexPub))
            {
                _logger.LogWarning("InitializeAsync - Missing required keys.");
                NotifyStateChanged();
                return; 
            }

            IsLoadingMessages = true;
            NotifyStateChanged();

            try
            {
                await LoadMessagesAsync();
                
                // Ensure we have a valid subscription
                EnsureActiveSubscription();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize messaging: {ex.Message}");
            }
            finally
            {
                IsLoadingMessages = false;
                NotifyStateChanged();
            }
        }

        public async Task LoadMessagesAsync()
        {
            if (string.IsNullOrEmpty(_currentUserPrivateKeyHex) || string.IsNullOrEmpty(_contactHexPub) || string.IsNullOrEmpty(_currentUserHexPub))
            {
                _logger.LogWarning("LoadMessagesAsync - Missing required keys.");
                return;
            }

            _directMessagesDictionary.Clear();
            NotifyStateChanged(); 

            try
            {
                await _relayService.LookupDirectMessagesForPubKeyAsync(
                    _contactHexPub,
                    DateTime.UtcNow.AddDays(-7),
                    100,
                    async eventMessage => await ProcessDirectMessage(eventMessage),
                    _currentUserHexPub
                );

                await _relayService.LookupDirectMessagesForPubKeyAsync(
                    _currentUserHexPub,
                    DateTime.UtcNow.AddDays(-7),
                    100,
                    async eventMessage => await ProcessDirectMessage(eventMessage),
                    _contactHexPub
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load messages: {ex.Message}");
            }
            finally
            {
                NotifyStateChanged(); 
            }
        }

        private async Task LoadNewMessagesAsync()
        {
            if (string.IsNullOrEmpty(_currentUserPrivateKeyHex) || string.IsNullOrEmpty(_contactHexPub) || string.IsNullOrEmpty(_currentUserHexPub))
            {
                _logger.LogWarning("LoadNewMessagesAsync - Missing required keys.");
                return;
            }

            DateTime sinceTime = DateTime.UtcNow.AddDays(-7);
            if (_directMessagesDictionary.Any())
            {
                sinceTime = _directMessagesDictionary.Values.Max(m => m.Timestamp);
            }

            try
            {
                await _relayService.LookupDirectMessagesForPubKeyAsync(
                    _currentUserHexPub,
                    sinceTime,
                    100,
                    async eventMessage => await ProcessDirectMessage(eventMessage),
                    _contactHexPub
                );

                await _relayService.LookupDirectMessagesForPubKeyAsync(
                    _contactHexPub,
                    sinceTime,
                    100,
                    async eventMessage => await ProcessDirectMessage(eventMessage),
                    _currentUserHexPub
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load new messages: {ex.Message}");
            }
            finally
            {
                NotifyStateChanged();
            }
        }

        private void EnsureActiveSubscription()
        {
            if (_subscriptionActive)
            {
                _logger.LogDebug("Message subscription already active, no need to resubscribe");
                return;
            }

            if (string.IsNullOrEmpty(_currentUserHexPub) || string.IsNullOrEmpty(_contactHexPub))
            {
                _logger.LogWarning("EnsureActiveSubscription - Missing required keys.");
                return;
            }

            try
            {
                _messageSubscription?.Dispose();
                var nostrClient = _nostrCommunicationFactory.GetOrCreateClient(_networkService);

                _messageSubscription = nostrClient.Streams.EventStream
                    .Where(ev => ev.Event?.Kind == NostrKind.EncryptedDm)
                    .Where(ev =>
                    {
                        if (ev.Event == null) return false;
                        string tagValue = ev.Event.Tags?.FindFirstTagValue(NostrEventTag.ProfileIdentifier);
                        bool isFromUserToContact = ev.Event.Pubkey == _currentUserHexPub && tagValue == _contactHexPub;
                        bool isFromContactToUser = ev.Event.Pubkey == _contactHexPub && tagValue == _currentUserHexPub;
                        return isFromUserToContact || isFromContactToUser;
                    })
                    .Subscribe(async messageEvent =>
                    {
                        await ProcessDirectMessage(messageEvent.Event);
                        NotifyStateChanged();
                    });
                
                _subscriptionActive = true;
                _logger.LogInformation("Real-time message subscription activated successfully");
            }
            catch (Exception ex)
            {
                _subscriptionActive = false;
                _logger.LogError($"Error establishing message subscription: {ex.Message}");
            }
        }

        private async Task ProcessDirectMessage(NostrEvent eventMessage)
        {
            if (eventMessage == null || string.IsNullOrEmpty(eventMessage.Content) || string.IsNullOrEmpty(_currentUserPrivateKeyHex))
            {
                return;
            }

            if (_directMessagesDictionary.ContainsKey(eventMessage.Id))
            {
                return;
            }

            bool isFromCurrentUser = eventMessage.Pubkey == _currentUserHexPub;
            string decryptedContent;

            try
            {
                string otherPartyPubkey = isFromCurrentUser ? _contactHexPub : eventMessage.Pubkey;

                decryptedContent = await _encryptionService.DecryptNostrContentAsync(
                    _currentUserPrivateKeyHex,
                    otherPartyPubkey,
                    eventMessage.Content
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to decrypt message {eventMessage.Id}: {ex.Message}");
                decryptedContent = "[Could not decrypt message]";
            }

            bool isDuplicate = _directMessagesDictionary.Values.Any(m =>
                m.Content == decryptedContent &&
                m.IsFromCurrentUser == isFromCurrentUser &&
                Math.Abs((m.Timestamp - eventMessage.CreatedAt.GetValueOrDefault()).TotalMinutes) < 1);
            
            if (isDuplicate)
            {
                return;
            }

            var directMessage = new DirectMessage
            {
                Id = eventMessage.Id,
                Content = decryptedContent,
                SenderPubkey = eventMessage.Pubkey,
                Timestamp = eventMessage.CreatedAt.GetValueOrDefault(DateTime.UtcNow),
                IsFromCurrentUser = isFromCurrentUser
            };

            _directMessagesDictionary[eventMessage.Id] = directMessage;
        }

        public async Task SendMessageAsync(string messageContent)
        {
            if (string.IsNullOrWhiteSpace(messageContent) || string.IsNullOrEmpty(_currentUserPrivateKeyHex) || string.IsNullOrEmpty(_contactHexPub))
            {
                _logger.LogWarning("SendMessageAsync - Missing required info or empty message.");
                return;
            }

            IsSendingMessage = true;
            NotifyStateChanged();

            try
            {
                // Ensure subscription is active before sending
                EnsureActiveSubscription();

                string encryptedContent = await _encryptionService.EncryptNostrContentAsync(
                    _currentUserPrivateKeyHex,
                    _contactHexPub,
                    messageContent
                );

                var sentMessageId = _relayService.SendDirectMessagesForPubKeyAsync(
                    _currentUserPrivateKeyHex,
                    _contactHexPub,
                    encryptedContent,
                    null 
                );

                var sentDirectMessage = new DirectMessage
                {
                    Id = sentMessageId,
                    Content = messageContent,
                    SenderPubkey = _currentUserHexPub,
                    Timestamp = DateTime.UtcNow,
                    IsFromCurrentUser = true
                };

                _directMessagesDictionary[sentMessageId] = sentDirectMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send message: {ex.Message}");
            }
            finally
            {
                IsSendingMessage = false;
                NotifyStateChanged();
            }
        }

        public async Task RefreshMessagesAsync()
        {
            if (IsRefreshing) return;

            IsRefreshing = true;
            NotifyStateChanged();

            try
            {
                // Only dispose and resubscribe if the current subscription is not active
                if (!_subscriptionActive)
                {
                    _messageSubscription?.Dispose();
                    _messageSubscription = null;
                }

                await LoadNewMessagesAsync();
                
                // Ensure we have a valid subscription
                EnsureActiveSubscription();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to refresh messages: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

        public void Dispose()
        {
            _messageSubscription?.Dispose();
            _subscriptionActive = false;
            GC.SuppressFinalize(this);
        }
    }
}