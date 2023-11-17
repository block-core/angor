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
    Task LookupProjectsInfoByPubKeysAsync<T>(Action<T> responseDataAction,params string[] nostrPubKey);
    Task RequestProjectEventsoByPubKeyAsync(string nostrPubKey, Action<NostrEvent> onResponseAction);

    Task LookupDirectMessagesForPubKeyAsync(string nostrPubKey, DateTime? since, Action<NostrEvent> onResponseAction);
    void CloseConnection();
}