using System.ComponentModel.DataAnnotations;

namespace Angor.Contexts.Data.Entities;

public class ProjectStage
{
    public string ProjectId { get; set; } = string.Empty;
    
    public int StageIndex { get; set; }
    
    public decimal AmountToRelease { get; set; }
    
    public long ReleaseDate { get; set; }
    
    public DateTime? ReleasedAt { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation property
    public Project? Project { get; set; }
}