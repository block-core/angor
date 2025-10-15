namespace Angor.Data.Documents.Models;

public abstract class BaseDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}