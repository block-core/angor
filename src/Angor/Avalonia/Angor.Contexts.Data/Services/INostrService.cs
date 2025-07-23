using Nostr.Client.Messages;
using Nostr.Client.Responses;

namespace Angor.Contexts.Data.Services;

public interface INostrService
{
    Task<List<NostrEventResponse>> GetEventsAsync(string subscriptionId, string[] receiversPubkeys, NostrKind[] kinds,
        string[]? sendersPubkeys = null, string[]? eventIds = null);
    Task ConnectToRelaysAsync();
    Task DisconnectFromRelaysAsync();
    void AddRelay(string relayUrl);
    void RemoveRelay(string relayUrl);
    List<string> GetConnectedRelays();
}