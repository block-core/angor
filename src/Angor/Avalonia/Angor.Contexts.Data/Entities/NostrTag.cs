namespace Angor.Contexts.Data.Entities;

public class NostrTag
{
    public string Name { get; set; } = null!; // Primary key
    public string EventId { get; set; } = null!; // Foreign key to NostrEvent
    public List<string> Content { get; set; } = new(); // List of strings for tag content

    // Navigation property
    public NostrEvent Event { get; set; } = null!;
}