namespace Angor.Contexts.Funding.Shared;

public record DirectMessage(string Id, string InvestorNostrPubKey, string Content, DateTime Created);