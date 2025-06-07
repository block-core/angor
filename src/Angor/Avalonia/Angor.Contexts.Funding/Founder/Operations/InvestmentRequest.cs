namespace Angor.Contexts.Funding.Founder.Operations;

internal record InvestmentRequest(DateTime CreatedOn, string InvestorNostrPubKey, string InvestmentTransactionHex, string EventId);