using Angor.Contexts.Funding.Founder;

namespace Angor.Contexts.Funding.Projects.Domain;

public class Investment
{
    public Guid Id { get; private set; }
    public ProjectId ProjectId { get; private set; }
    public string InvestorPubKey { get; private set; }
    public Amount Amount { get; private set; }
    public DateTime InvestmentDate { get; private set; }
    public string TransactionId { get; private set; }
    public InvestmentStatus Status { get; private set; }
    
    public static Investment Create(ProjectId projectId, string investorId, Amount amount, string transactionId)
    {
        return new Investment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            InvestorPubKey = investorId,
            Amount = amount,
            InvestmentDate = DateTime.UtcNow,
            Status = InvestmentStatus.Pending,
            TransactionId = transactionId,
        };
    }
}