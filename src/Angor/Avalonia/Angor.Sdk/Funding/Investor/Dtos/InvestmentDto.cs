using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;

namespace Angor.Sdk.Funding.Investor.Dtos;

public class InvestmentDto
{
    public ProjectId ProjectId { get; set; } = null!;
    public string InvestorKey { get; set; } = string.Empty;
    public long AmountInSats { get; set; }
    public string TransactionId { get; set; } = string.Empty;
}