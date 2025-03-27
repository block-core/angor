namespace Angor.Projects.Domain;

public class Project
{
    public ProjectId Id { get; set; }
    public string Name { get; set; }
    public Uri Picture { get; set; }
    public Uri Icon { get; set; }
    public string ShortDescription { get; set; }
    public long TargetAmount { get; set; }
    public DateTime StartingDate { get; set; }
    public IEnumerable<Stage> Stages { get; set; }
    public string NostrPubKey { get; set; }
    public TimeSpan PenaltyDuration { get; set; }
    public Uri InformationUri { get; set; }
    public string FounderKey { get; set; }
    public string FounderRecoveryKey { get; set; }
    public DateTime ExpiryDate { get; set; }

    public override string ToString()
    {
        return Name;
    }
}