using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Investment.Commands.CreateInvestment;

public record PendingInvestment(
    ProjectId ProjectId, 
    string InvestorPubKey, 
    long Amount, 
    string TransactionId,
    string SignedTransactionHex);