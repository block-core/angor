using Angor.Contexts.Projects.Domain;

namespace Angor.Contexts.Projects.Application.Dtos;

public class InvestmentDto
{
    public ProjectId ProjectId { get; set; }
    public string InvestorKey { get; set; }
    public long Amount { get; set; }
    public string TransactionId { get; set; }
}