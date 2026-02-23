namespace Angor.Shared.Models;

public class ProjectIndexerData
{
    public string FounderKey { get; set; } = string.Empty;
    public string ProjectIdentifier { get; set; } = string.Empty;
    public long CreatedOnBlock { get; set; }
    public string NostrEventId { get; set; } = string.Empty;

    public string TrxId { get; set; } = string.Empty;
    public long? TotalInvestmentsCount { get; set; }
}