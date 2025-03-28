using Angor.Projects.Domain;

namespace Angor.Projects.Application.Dtos;

public class InvestmentDto
{
    public ProjectId ProjectId { get; set; }
    public object InvestorKey { get; set; }
    public object Amount { get; set; }
    public string TransactionId { get; set; }
}