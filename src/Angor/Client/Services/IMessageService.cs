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
        event Action OnChange;

        Task InitializeAsync(string currentUserPrivateKeyHex, string currentUserNpub, string contactHexPub);
        Task LoadMessagesAsync();
        Task SendMessageAsync(string messageContent);
        Task RefreshMessagesAsync();
        void SetKeys(string currentUserPrivateKeyHex, string currentUserNpub, string contactHexPub);
    }
}
