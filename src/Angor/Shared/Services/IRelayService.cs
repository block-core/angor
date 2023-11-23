using Angor.Shared.Models;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Metadata;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public interface IRelayService
{
    Task ConnectToRelaysAsync();
    void RegisterOKMessageHandler(string eventId, Action<NostrOkResponse> action);
    Task<string> AddProjectAsync(ProjectInfo project, string nsec);
    Task<string> CreateNostrProfileAsync(NostrMetadata metadata, string nsec);
    Task<string> DeleteProjectAsync(string eventId, string hexPrivateKey);
    void LookupProjectsInfoByPubKeys<T>(Action<T> responseDataAction, Action? OnEndOfStreamAction,
        params string[] nostrPubKey);
    Task RequestProjectCreateEventsByPubKeyAsync(string nostrPubKey, Action<NostrEvent> onResponseAction);

    Task LookupDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, int? limit,
        Action<NostrEvent> onResponseAction);
    void CloseConnection();
}