namespace Angor.Contexts.Data.Entities;

public class NostrEvent
{
    public string Id { get; set; } = null!;
    public string PubKey { get; set; } = null!; // Foreign key
    public int Kind { get; set; }
    public string Content { get; set; } = null!;
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string Signature { get; set; } = null!;
    
    // Navigation property
    public NostrUser User { get; set; } = null!;
}

