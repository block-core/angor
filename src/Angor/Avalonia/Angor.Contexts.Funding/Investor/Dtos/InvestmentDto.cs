using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;

namespace Angor.Contexts.Funding.Investor.Dtos;

public class InvestmentDto
{
    public ProjectId ProjectId { get; set; }
    public string InvestorKey { get; set; }
    public long AmountInSats { get; set; }
    public string TransactionId { get; set; }
}