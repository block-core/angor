using System.ComponentModel.DataAnnotations;

namespace Angor.Contexts.Data.Entities;

public class Project
{
    [Key]
    public string ProjectId { get; set; } = string.Empty;
    
    public string NostrPubKey { get; set; } = string.Empty;
    
    public string ProjectSenderAddress { get; set; } = string.Empty;
    
    public decimal TargetAmount { get; set; }
    
    public long StartDate { get; set; }
    
    public long EndDate { get; set; }
    
    public long PenaltyDays { get; set; }
    
    public long ExpiryDate { get; set; }
    
    public string ProjectSeekerSecretHash { get; set; } = string.Empty;
    
    public string NostrEventId { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public NostrUser? NostrUser { get; set; }
    public NostrEvent? NostrEvent { get; set; }
    public ICollection<ProjectStage> Stages { get; set; } = new List<ProjectStage>();
}