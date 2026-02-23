using Angor.Shared.Models;

namespace AngorApp.Model.Projects;

public class Project : IProject
{
    public string NostrNpubKeyHex { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Uri Banner { get; set; } = null!;
    public Uri Picture { get; set; } = null!;
    public string ShortDescription { get; set; } = string.Empty;
    public string BitcoinAddress { get; set; } = string.Empty;
    public IAmountUI TargetAmount { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public IEnumerable<IStage> Stages { get; set; } = [];
    public string NpubKey { get; set; } = string.Empty;
    public TimeSpan PenaltyDuration { get; set; }
    public Uri InformationUri { get; set; } = null!;
    public DateTime EndDate { get; set; }
    public int TotalInvestors { get; set; }
    public IAmountUI TotalRaised { get; set; } = null!;

    public int Version { get; set; } = 2;
    public ProjectType ProjectType { get; set; } = ProjectType.Invest;
    public List<DynamicStagePattern> DynamicStagePatterns { get; set; } = new();

    public override string ToString()
    {
        return Name;
    }
}
