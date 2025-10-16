namespace Angor.Data.Documents.Models;

public class ProjectDocument : BaseDocument
{
    public new string Id { get; set; }
    public string Name { get; set; }
    public Uri? Picture { get; set; }
    public string ShortDescription { get; set; }
    public long TargetAmount { get; set; }
    public DateTime StartingDate { get; set; }
    public IEnumerable<StageDocument> Stages { get; set; }
    public string NostrPubKey { get; set; }
    public TimeSpan PenaltyDuration { get; set; }
    public Uri? InformationUri { get; set; }
    public string FounderKey { get; set; }
    public string FounderRecoveryKey { get; set; }
    public DateTime ExpiryDate { get; set; }
    public Uri? Banner { get; set; }
    public DateTime EndDate { get; set; }
}