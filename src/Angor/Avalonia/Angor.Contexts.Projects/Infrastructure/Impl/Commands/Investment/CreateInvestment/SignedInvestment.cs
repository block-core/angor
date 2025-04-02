using Angor.Contexts.Projects.Domain;

namespace Angor.Contexts.Projects.Infrastructure.Impl.Commands.CreateInvestment;

public record SignedInvestment(
    ProjectId ProjectId, 
    string InvestorPubKey, 
    long Amount, 
    string TransactionId, 
    string SignedTransactionHex,
    List<string> FounderSignatures);