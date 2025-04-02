using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Investment.Dtos;

public class InvestmentDto
{
    public ProjectId ProjectId { get; set; }
    public string InvestorKey { get; set; }
    public long Amount { get; set; }
    public string TransactionId { get; set; }
}