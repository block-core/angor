using Angor.Contexts.Projects.Domain;

namespace Angor.Contexts.Projects.Infrastructure.Impl.Commands;

public record PendingInvestment(
    ProjectId ProjectId, 
    string InvestorPubKey, 
    long Amount, 
    string TransactionId,
    string SignedTransactionHex);