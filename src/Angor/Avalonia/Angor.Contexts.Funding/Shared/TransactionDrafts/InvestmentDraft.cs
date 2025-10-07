using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Shared.TransactionDrafts;

public record InvestmentDraft(string InvestorKey) : TransactionDraft
{
    public Amount MinerFee { get; set; } = new Amount(-1);
    public Amount AngorFee { get; set; } = new Amount(-1);
}