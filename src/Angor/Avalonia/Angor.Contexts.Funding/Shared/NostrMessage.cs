namespace Angor.Contexts.Funding.Founder.Operations;

public record NostrMessage(string Id, string InvestorNostrPubKey, string Content, DateTime Created);