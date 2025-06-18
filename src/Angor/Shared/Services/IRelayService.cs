using Angor.Shared.Models;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Metadata;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public interface IRelayService
{
    void LookupNostrProfileForNPub(Action<string, ProjectMetadata> onResponse, Action onEndOfStream, params string[] npub);
    Task<string> AddProjectAsync(ProjectInfo project, string nsec,Action<NostrOkResponse> action);
    Task<string> CreateNostrProfileAsync(NostrMetadata metadata, string nsec, Action<NostrOkResponse> action);
    Task<string> DeleteProjectAsync(string eventId, string hexPrivateKey);
    void LookupProjectsInfoByEventIds<T>(Action<T> responseDataAction, Action? OnEndOfStreamAction, params string[] nostrEventIds);
    void RequestProjectCreateEventsByPubKey(Action<NostrEvent> onResponseAction, Action? onEoseAction,params string[] nostrPubKeys);

    Task LookupSignaturesDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit, Action<NostrEvent> onResponseAction);
    
    void LookupDirectMessagesForPubKey(string nostrPubKey, DateTime? since, int? limit,
        Func<NostrEvent, Task> onResponseAction, string[]? sendersPubkey = null, bool keepActive = false);
    
    string SendDirectMessagesForPubKeyAsync(string senderNosterPrivateKey, string nostrPubKey, string encryptedMessage,
        Action<NostrOkResponse> onResponseAction);
    
    void DisconnectSubscription(string subscription);
}