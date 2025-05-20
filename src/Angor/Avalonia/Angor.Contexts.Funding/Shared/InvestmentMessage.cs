namespace Angor.Contexts.Funding.Shared;

public record InvestmentMessage(string Id, string InvestorNostrPubKey, string Content, DateTime Created);