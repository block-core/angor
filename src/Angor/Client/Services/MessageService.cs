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
        private string _currentUserPrivateKeyHex;
        private string _currentUserNpub;
        private string _currentUserHexPub;
        private string _contactHexPub;
        private bool _subscriptionActive = false;

        private DateTime _sinceTime = DateTime.UtcNow.AddDays(-7);

        public IReadOnlyList<DirectMessage> DirectMessages => _directMessagesDictionary.Values
            .OrderBy(m => m.Timestamp)
            .ToList()
            .AsReadOnly();
        public bool IsLoadingMessages { get; private set; }
        public bool IsSendingMessage { get; private set; }
        public bool IsRefreshing { get; private set; }
        public bool IsSubscriptionActive => _subscriptionActive;

        public event Action OnStateChange;

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
            _sinceTime = DateTime.UtcNow.AddDays(-7);

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

        public void DisconnectSubscriptions()
        {
            if (_subscriptionActive)
            {
                _relayService.DisconnectSubscription(_currentUserHexPub);
                _relayService.DisconnectSubscription(_contactHexPub);

                _sinceTime = DateTime.UtcNow.AddDays(-7);
                _subscriptionActive = false;
                _directMessagesDictionary.Clear();
            }
        }

        public async Task LoadMessagesAsync()
        {
            if (string.IsNullOrEmpty(_currentUserPrivateKeyHex) || string.IsNullOrEmpty(_contactHexPub) || string.IsNullOrEmpty(_currentUserHexPub))
            {
                _logger.LogWarning("LoadNewMessagesAsync - Missing required keys.");
                return;
            }

            try
            {
                DisconnectSubscriptions();

                NotifyStateChanged();

                await _relayService.LookupDirectMessagesForPubKeyAsync(
                    _currentUserHexPub,
                    _sinceTime,
                    100,
                    async eventMessage => await ProcessDirectMessage(eventMessage),
                    _contactHexPub,
                    true
                );

                await _relayService.LookupDirectMessagesForPubKeyAsync(
                    _contactHexPub,
                    _sinceTime,
                    100,
                    async eventMessage => await ProcessDirectMessage(eventMessage),
                    _currentUserHexPub,
                    true
                );

                _subscriptionActive = true;
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

            var directMessage = new DirectMessage
            {
                Id = eventMessage.Id,
                Content = decryptedContent,
                SenderPubkey = eventMessage.Pubkey,
                Timestamp = eventMessage.CreatedAt.GetValueOrDefault(DateTime.UtcNow),
                IsFromCurrentUser = isFromCurrentUser
            };

            _directMessagesDictionary[eventMessage.Id] = directMessage;

            _sinceTime = directMessage.Timestamp;

            NotifyStateChanged();
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

        private void NotifyStateChanged() => OnStateChange?.Invoke();

        public void Dispose()
        {
            DisconnectSubscriptions();
            _subscriptionActive = false;
            GC.SuppressFinalize(this);
        }
    }
}