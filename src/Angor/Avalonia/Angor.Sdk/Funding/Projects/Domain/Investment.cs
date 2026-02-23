using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Shared;

namespace Angor.Sdk.Funding.Projects.Domain;

public class Investment
{
    public Guid Id { get; private set; }
    public ProjectId ProjectId { get; private set; } = null!;
    public string InvestorPubKey { get; private set; } = string.Empty;
    public Amount Amount { get; private set; } = null!;
    public DateTime InvestmentDate { get; private set; }
    public string TransactionId { get; private set; } = string.Empty;
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
            Status = InvestmentStatus.PendingFounderSignatures,
            TransactionId = transactionId,
        };
    }
}