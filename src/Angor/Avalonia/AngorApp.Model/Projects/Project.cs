using Angor.Shared.Models;

namespace AngorApp.Model.Projects;

public class OldProject : IOldProject
{
    public string NostrNpubKeyHex { get; set; }
    public string Id { get; set; }
    public string Name { get; set; }
    public Uri Banner { get; set; }
    public Uri Picture { get; set; }
    public string ShortDescription { get; set; }
    public string BitcoinAddress { get; set; }
    public IAmountUI TargetAmount { get; set; }
    public DateTime StartDate { get; set; }
    public IEnumerable<IStage> Stages { get; set; }
    public string NpubKey { get; set; }
    public TimeSpan PenaltyDuration { get; set; }
    public Uri InformationUri { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalInvestors { get; set; }
    public IAmountUI TotalRaised { get; set; }

    public int Version { get; set; } = 2;
    public ProjectType ProjectType { get; set; } = ProjectType.Invest;
    public List<DynamicStagePattern> DynamicStagePatterns { get; set; } = new();

    public override string ToString()
    {
        return Name;
    }
}
