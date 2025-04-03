using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Investor.Requests.CreateInvestment;

public record PendingInvestment(
    ProjectId ProjectId, 
    string InvestorPubKey, 
    Amount Amount, 
    string TransactionId,
    string SignedTransactionHex);