namespace Angor.Sdk.Funding.Founder.Domain;

internal record InvestmentRequest(DateTime CreatedOn, string InvestorNostrPubKey, string InvestmentTransactionHex, string EventId);