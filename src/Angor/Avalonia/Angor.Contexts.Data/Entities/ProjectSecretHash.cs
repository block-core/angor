using System.ComponentModel.DataAnnotations;

namespace Angor.Contexts.Data.Entities;

public class ProjectSecretHash
{
    [Key]
    public int Id { get; set; }
    
    public string ProjectId { get; set; } = string.Empty;
    
    public string SecretHash { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation property
    public Project? Project { get; set; }
}