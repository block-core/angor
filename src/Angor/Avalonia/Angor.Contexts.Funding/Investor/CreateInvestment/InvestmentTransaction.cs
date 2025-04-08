using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Investor.CreateInvestment;

public record InvestmentTransaction(string InvestorKey, string SignedTxHex, string TransactionId, Amount TotalFee);