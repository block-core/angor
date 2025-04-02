namespace Angor.Projects.Domain;

public class Project
{
    public ProjectId Id { get; set; }
    public string Name { get; set; }
    public Uri Picture { get; set; }
    public Uri Icon { get; set; }
    public string ShortDescription { get; set; }
    public decimal TargetAmount { get; set; }
    public DateOnly StartingDate { get; set; }
    public IEnumerable<Stage> Stages { get; set; }
    public string NostrPubKey { get; set; }
    public TimeSpan PenaltyDuration { get; set; }
    public Uri InformationUri { get; set; }

    public override string ToString()
    {
        return Name;
    }
}