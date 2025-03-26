namespace Angor.Projects.Domain;

public record class Amount(long Sats);

public class Investment
{
    public Guid Id { get; private set; }
    public ProjectId ProjectId { get; private set; }
    public string InvestorPubKey { get; private set; }
    public long AmountInSatoshis { get; private set; }
    public DateTime InvestmentDate { get; private set; }
    public string TransactionId { get; private set; }
    public InvestmentStatus Status { get; private set; }
    
    public static Investment Create(ProjectId projectId, string investorId, long amountInSatoshis)
    {
        return new Investment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            InvestorPubKey = investorId,
            AmountInSatoshis = amountInSatoshis,
            InvestmentDate = DateTime.UtcNow,
            Status = InvestmentStatus.Pending
        };
    }
    
    public void ConfirmTransaction(string transactionId)
    {
        TransactionId = transactionId;
        Status = InvestmentStatus.Confirmed;
    }
}

public enum InvestmentStatus
{
    Pending,
    Confirmed,
    Failed
}