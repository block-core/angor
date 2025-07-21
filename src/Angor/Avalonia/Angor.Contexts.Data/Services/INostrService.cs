using Nostr.Client.Messages;
using Nostr.Client.Responses;

namespace Angor.Contexts.Data.Services;

public interface INostrService
{
    Task<List<NostrEventResponse>> GetEventsByKindAsync(NostrKind kind, params string[] eventIds);
    Task<List<NostrEventResponse>> GetEventsByKindAsync(NostrKind kind, int timeoutSeconds = 10, params string[] eventIds);
    Task ConnectToRelaysAsync();
    Task DisconnectFromRelaysAsync();
    void AddRelay(string relayUrl);
    void RemoveRelay(string relayUrl);
    List<string> GetConnectedRelays();
}