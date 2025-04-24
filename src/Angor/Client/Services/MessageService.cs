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
using Nostr.Client.Messages.Contacts; // Added for NostrEventTag

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
                _currentUserHexPub = null; // Or handle error appropriately
            }
        }

        public async Task InitializeAsync(string currentUserPrivateKeyHex, string currentUserNpub, string contactHexPub)
        {
            SetKeys(currentUserPrivateKeyHex, currentUserNpub, contactHexPub);

            if (string.IsNullOrEmpty(_currentUserPrivateKeyHex) || string.IsNullOrEmpty(_contactHexPub) || string.IsNullOrEmpty(_currentUserHexPub))
            {
                _logger.LogWarning("InitializeAsync - Missing required keys.");
                NotifyStateChanged();
                return; // Don't proceed if keys are missing
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
                // Optionally re-throw or handle error state
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

            _directMessages.Clear();
            NotifyStateChanged(); // Clear UI immediately

            try
            {
                // Load messages sent FROM current user TO contact
                await _relayService.LookupDirectMessagesForPubKeyAsync(
                    _contactHexPub,
                    DateTime.UtcNow.AddDays(-7),
                    100,
                    async eventMessage => await ProcessDirectMessage(eventMessage),
                    _currentUserHexPub
                );

                // Load messages sent FROM contact TO current user
                await _relayService.LookupDirectMessagesForPubKeyAsync(
                    _currentUserHexPub,
                    DateTime.UtcNow.AddDays(-7),
                    100,
                    async eventMessage => await ProcessDirectMessage(eventMessage),
                    _contactHexPub
                );

                _directMessages = _directMessages.OrderBy(m => m.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load messages: {ex.Message}");
            }
            finally
            {
                NotifyStateChanged(); // Update UI with loaded messages or empty state
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

            if (_directMessages.Any(m => m.Id == eventMessage.Id))
            {
                return; // Skip duplicates by ID
            }

            bool isFromCurrentUser = eventMessage.Pubkey == _currentUserHexPub;
            string decryptedContent;

            try
            {
                string partnerPubKeyHex = isFromCurrentUser
                    ? eventMessage.Tags?.FindFirstTagValue(NostrEventTag.ProfileIdentifier)
                    : eventMessage.Pubkey;

                if (string.IsNullOrEmpty(partnerPubKeyHex))
                {
                    _logger.LogWarning($"Could not determine partner pubkey for message {eventMessage.Id}");
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
                _logger.LogError($"Failed to decrypt message {eventMessage.Id}: {ex.Message}");
                decryptedContent = "[Could not decrypt message]";
            }

            // More robust duplicate check (content, sender, time proximity)
            if (_directMessages.Any(m =>
                m.Content == decryptedContent &&
                m.IsFromCurrentUser == isFromCurrentUser &&
                Math.Abs((m.Timestamp - eventMessage.CreatedAt.GetValueOrDefault()).TotalMinutes) < 1))
            {
                return; // Skip likely duplicates
            }

            var directMessage = new DirectMessage
            {
                Id = eventMessage.Id,
                Content = decryptedContent,
                SenderPubkey = eventMessage.Pubkey,
                Timestamp = eventMessage.CreatedAt.GetValueOrDefault(DateTime.UtcNow),
                IsFromCurrentUser = isFromCurrentUser
            };

            _directMessages.Add(directMessage);
            _directMessages = _directMessages.OrderBy(m => m.Timestamp).ToList();
            // NotifyStateChanged is called by the subscriber or LoadMessages
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
                    null // Assuming tags are handled by SendDirectMessagesForPubKeyAsync or not needed here
                );

                // Optimistically add to UI - consider waiting for confirmation or handling potential failures
                var sentDirectMessage = new DirectMessage
                {
                    Id = sentMessageId, // Note: This might not be the final confirmed ID from relays
                    Content = messageContent, // Show the original content
                    SenderPubkey = _currentUserHexPub,
                    Timestamp = DateTime.UtcNow,
                    IsFromCurrentUser = true
                };

                _directMessages.Add(sentDirectMessage);
                _directMessages = _directMessages.OrderBy(m => m.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send message: {ex.Message}");
                // Optionally notify UI of failure
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
                _messageSubscription?.Dispose(); // Unsubscribe before reloading
                _messageSubscription = null;

                await LoadMessagesAsync(); // Reloads messages

                SubscribeToMessages(); // Resubscribe after loading
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
