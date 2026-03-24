namespace Angor.Sdk.Funding.Founder.Domain;

public record Investment(string EventId, DateTime CreatedOn, string InvestmentTransactionHex, string InvestorNostrPubKey, long Amount, InvestmentStatus Status);