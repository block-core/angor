using Nostr.Client.Responses;

namespace Angor.Contexts.Data.Services;

public interface INostrService
{
    Task<List<NostrEventResponse>> GetEventsByKindAsync(int kind, params string[] eventIds);
    Task<List<NostrEventResponse>> GetEventsByKindAsync(int kind, int timeoutSeconds = 10, params string[] eventIds);
    Task ConnectToRelaysAsync();
    Task DisconnectFromRelaysAsync();
    void AddRelay(string relayUrl);
    void RemoveRelay(string relayUrl);
    List<string> GetConnectedRelays();
}