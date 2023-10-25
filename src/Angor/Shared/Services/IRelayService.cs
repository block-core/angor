using Angor.Shared.Models;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public interface IRelayService
{
    Task ConnectToRelaysAsync();
    void RegisterOKMessageHandler(string eventId, Action<NostrOkResponse> action);
    Task<string> AddProjectAsync(ProjectInfo project, string nsec);
    Task RequestProjectDataAsync<T>(Action<T> responseDataAction,params string[] nostrPubKey);
    void CloseConnection();
}