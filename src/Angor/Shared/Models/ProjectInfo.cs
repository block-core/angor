namespace Angor.Shared.Models;

/// <summary>
/// Encapsulates the public information related to an investment project.
/// This data, when combined with additional keys owned by an investor, facilitates the creation of an investment transaction.
/// </summary>
public class ProjectInfo
{
    public int ProjectIndex { get; set; }
    public string FounderKey { get; set; }
    public string FounderRecoveryKey { get; set; }
    public string ProjectIdentifier { get; set; }
    
    public string NostrPubKey { get; set; }
    public DateTime StartDate { get; set; }
    public int PenaltyDays { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal TargetAmount { get; set; }
    public List<Stage> Stages { get; set; } = new();
    public string TransactionId { get; set; }
    public ProjectSeeders ProjectSeeders { get; set; } = new();
}