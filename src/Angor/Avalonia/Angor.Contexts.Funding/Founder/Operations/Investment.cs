namespace Angor.Contexts.Funding.Founder.Operations;

public record Investment(string EventId, DateTime CreatedOn, string InvestmentTransactionHex, string InvestorNostrPubKey, long Amount, InvestmentStatus Status);