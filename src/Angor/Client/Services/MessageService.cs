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

        private List<DirectMessage> _directMessages = new();
        private IDisposable _messageSubscription;
        private string _currentUserPrivateKeyHex;
        private string _currentUserNpub;
        private string _currentUserHexPub;
        private string _contactHexPub;

        private readonly object _messagesLock = new object();

        public IReadOnlyList<DirectMessage> DirectMessages => _directMessages.AsReadOnly();
        public bool IsLoadingMessages { get; private set; }
        public bool IsSendingMessage { get; private set; }
        public bool IsRefreshing { get; private set; }

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
                SubscribeToMessages();
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

                lock (_messagesLock)
                {
                    _directMessages = _directMessages.OrderBy(m => m.Timestamp).ToList();
                }
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

        private void SubscribeToMessages()
        {
            if (string.IsNullOrEmpty(_currentUserHexPub) || string.IsNullOrEmpty(_contactHexPub))
            {
                _logger.LogWarning("SubscribeToMessages - Missing required keys.");
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
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in SubscribeToMessages: {ex.Message}");
            }
        }

        private async Task ProcessDirectMessage(NostrEvent eventMessage)
        {
            if (eventMessage == null || string.IsNullOrEmpty(eventMessage.Content) || string.IsNullOrEmpty(_currentUserPrivateKeyHex))
            {
                return;
            }

            string eventId = eventMessage.Id;
            bool isFromCurrentUser = eventMessage.Pubkey == _currentUserHexPub;
            string decryptedContent;
            string recipientPubkey = null;

            try
            {
                string partnerPubKeyHex;
                if (isFromCurrentUser)
                {
                    partnerPubKeyHex = eventMessage.Tags?.FindFirstTagValue(NostrEventTag.ProfileIdentifier);
                    recipientPubkey = partnerPubKeyHex;
                }
                else
                {
                    partnerPubKeyHex = eventMessage.Pubkey; 
                    recipientPubkey = _currentUserHexPub; 
                }

                if (string.IsNullOrEmpty(partnerPubKeyHex))
                {
                    _logger.LogWarning($"Could not determine partner pubkey for message {eventId}");
                    decryptedContent = "[Error: Missing recipient/sender info]";
                }
                else
                {
                    decryptedContent = await _encryptionService.DecryptNostrContentAsync(
                        _currentUserPrivateKeyHex,
                        partnerPubKeyHex,
                        eventMessage.Content
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to decrypt message {eventId}: {ex.Message}");
                decryptedContent = "[Could not decrypt message]";
            }

            lock (_messagesLock)
            {
                if (_directMessages.Any(m => m.Id == eventId))
                {
                    return; // Already processed this event ID.
                }

                if (_directMessages.Any(m =>
                    m.Content == decryptedContent &&
                    m.IsFromCurrentUser == isFromCurrentUser &&
                    Math.Abs((m.Timestamp - eventMessage.CreatedAt.GetValueOrDefault()).TotalMinutes) < 1))
                {
                    _logger.LogInformation($"Message with ID {eventId} skipped due to content/timestamp similarity with an existing message.");
                    return; 
                }

                var directMessage = new DirectMessage
                {
                    Id = eventId,
                    Content = decryptedContent,
                    SenderPubkey = eventMessage.Pubkey,
                    RecipientPubkey = recipientPubkey,
                    Timestamp = eventMessage.CreatedAt.GetValueOrDefault(DateTime.UtcNow),
                    IsFromCurrentUser = isFromCurrentUser
                };

                _directMessages.Add(directMessage);
                _directMessages = _directMessages.OrderBy(m => m.Timestamp).ToList();
            }
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
                    RecipientPubkey = _contactHexPub,
                    Timestamp = DateTime.UtcNow,
                    IsFromCurrentUser = true
                };

                lock (_messagesLock)
                {
                    if (!_directMessages.Any(m => m.Id == sentMessageId))
                    {
                        _directMessages.Add(sentDirectMessage);
                        _directMessages = _directMessages.OrderBy(m => m.Timestamp).ToList();
                    }
                }
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
                _messageSubscription?.Dispose(); 
                _messageSubscription = null;

                await LoadMessagesAsync();

                SubscribeToMessages();
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
            GC.SuppressFinalize(this);
        }
    }
}
