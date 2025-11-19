namespace Angor.Contexts.Funding.Shared;

public record DirectMessage(string Id, string SenderNostrPubKey, string Content, DateTime Created);