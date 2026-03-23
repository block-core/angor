using Nostr.Client.Messages;

namespace Angor.Shared.Models;
public class SignServiceLookupItem
{
    public DateTime EventCreatedAt { get; set; }

    public string ProfileIdentifier { get; set; }

    public string EventIdentifier { get; set; }

    public NostrEvent NostrEvent { get; set; }
}