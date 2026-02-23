using Nostr.Client.Messages;

namespace Angor.Shared.Models;
public class SignServiceLookupItem
{
    public DateTime EventCreatedAt { get; set; }

    public string ProfileIdentifier { get; set; } = string.Empty;

    public string EventIdentifier { get; set; } = string.Empty;

    public NostrEvent NostrEvent { get; set; } = null!;
}