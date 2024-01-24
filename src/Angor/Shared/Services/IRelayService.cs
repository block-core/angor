using Angor.Shared.Models;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Metadata;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public interface IRelayService
{
    Task<string> AddProjectAsync(ProjectInfo project, string nsec,Action<NostrOkResponse> action);
    Task<string> CreateNostrProfileAsync(NostrMetadata metadata, string nsec,Action<NostrOkResponse> action);
    Task<string> DeleteProjectAsync(string eventId, string hexPrivateKey);
    void LookupProjectsInfoByPubKeys<T>(Action<T> responseDataAction, Action? OnEndOfStreamAction,
        params string[] nostrPubKey);
    void RequestProjectCreateEventsByPubKey(Action<NostrEvent> onResponseAction, Action? onEoseAction,params string[] nostrPubKeys);

    Task LookupDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit, Action<NostrEvent> onResponseAction);
    void CloseConnection();
}