using Nostr.Client.Keys;
using Nostr.Client.Messages;

namespace Angor.Shared.Utilities;

public interface INostrNip59Actions
{
    NostrKind WrappedMessageKind => (NostrKind)1059;
    NostrKind InternalDMMessageKind => (NostrKind)14;
    
    Task<NostrEvent> SealEvent(NostrEvent rumor, NostrPrivateKey privateKey, string recipientNpub);

    Task<NostrEvent> WrapEventAsync(NostrEvent nostrEvent, string recipeintNpub,
        string? subject = null, string? replyTo = null);

    Task<NostrEvent?> UnwrapEventAsync(NostrEvent signed, string recipientNsec);
}