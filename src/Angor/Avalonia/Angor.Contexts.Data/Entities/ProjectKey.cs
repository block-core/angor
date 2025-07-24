using System.ComponentModel.DataAnnotations;

namespace Angor.Contexts.Data.Entities;

public class ProjectKey
{
    [Key]
    public required string FounderKey { get; set; }
    public Guid WalletId { get; set; }
    public int Index { get; set; }
    public required string ProjectId { get; set; }
    public required string NostrPubKey { get; set; } 
    public required string FounderRecoveryKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}