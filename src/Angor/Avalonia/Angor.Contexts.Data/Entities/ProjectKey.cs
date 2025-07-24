using System.ComponentModel.DataAnnotations;

namespace Angor.Contexts.Data.Entities;

public class ProjectKey
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid WalletId { get; set; }
    
    public string ProjectId { get; set; } = string.Empty;
    
    public string NostrPubKey { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}