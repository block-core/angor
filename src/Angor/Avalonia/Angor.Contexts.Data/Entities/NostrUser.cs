namespace Angor.Contexts.Data.Entities;

public class NostrUser
{
    public string ProfileEventId { get; set; }
    public string PubKey { get; set; } = null!; // Primary key (hex public key)
    public string? DisplayName { get; set; }
    public string? About { get; set; }
    public string? Picture { get; set; }
    public string? Website { get; set; }
    public string? Nip05 { get; set; } // NIP-05 verification
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    
    // Navigation properties
    public ICollection<NostrEvent> Events { get; set; } = new List<NostrEvent>();
}
