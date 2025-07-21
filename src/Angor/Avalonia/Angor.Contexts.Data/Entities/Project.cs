using System.ComponentModel.DataAnnotations;

namespace Angor.Contexts.Data.Entities;

public class Project
{
    [Key]
    public string ProjectId { get; set; } = string.Empty;
    
    public string NostrPubKey { get; set; } = string.Empty;
    
    public string ProjectReceiveAddress { get; set; } = string.Empty;
    
    public decimal TargetAmount { get; set; }
    
    public DateTime FundingStartDate { get; set; }
    
    public DateTime FundingEndDate { get; set; }
    
    public long PenaltyDays { get; set; }
    
    public DateTime ExpiryDate { get; set; }
    
    public string ProjectInfoEventId { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    public int LeadInvestorsThreshold { get; set; }
    
    // Navigation properties
    public NostrUser? NostrUser { get; set; }
    public NostrEvent? NostrEvent { get; set; }
    public ICollection<ProjectStage> Stages { get; set; } = new List<ProjectStage>();
    public ICollection<ProjectSecretHash> SecretHashes { get; set; } = new List<ProjectSecretHash>();
}