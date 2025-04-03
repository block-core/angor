using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Investor.Requests.CreateInvestment;

public record SignedInvestment(
    ProjectId ProjectId, 
    string InvestorPubKey, 
    Amount Amount, 
    string TransactionId, 
    string SignedTransactionHex,
    List<string> FounderSignatures);