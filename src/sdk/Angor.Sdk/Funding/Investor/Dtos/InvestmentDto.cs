using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;

using Angor.Primitives;

namespace Angor.Sdk.Funding.Investor.Dtos;

public class InvestmentDto
{
    public ProjectId ProjectId { get; set; }
    public string InvestorKey { get; set; }
    public long AmountInSats { get; set; }
    public string TransactionId { get; set; }
}