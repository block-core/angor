namespace Angor.Shared.Services;

public class ProjectIndexerData
{
    public string FounderKey { get; set; }
    public string ProjectIdentifier { get; set; }
    public long CreatedOnBlock { get; set; }
    public string NostrEventId { get; set; }

    public string TrxId { get; set; }
    public long? TotalInvestmentsCount { get; set; }
}