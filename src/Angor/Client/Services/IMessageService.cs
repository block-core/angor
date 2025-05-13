using Angor.Client.Models;
using Nostr.Client.Messages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Angor.Client.Services
{
    public interface IMessageService : IDisposable
    {
        IReadOnlyList<DirectMessage> DirectMessages { get; }
        bool IsLoadingMessages { get; }
        bool IsSendingMessage { get; }
        bool IsRefreshing { get; }
        bool IsSubscriptionActive { get; }
        event Action OnStateChange;

        Task InitializeAsync(string currentUserPrivateKeyHex, string otherUserHexPub);
        Task LoadMessagesAsync();
        Task SendMessageAsync(string messageContent);
        void SetKeys(string currentUserPrvKeyHex, string otherUserHexPub);
        void DisconnectSubscriptions();
    }
}
