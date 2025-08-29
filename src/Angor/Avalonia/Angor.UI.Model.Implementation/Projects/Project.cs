namespace Angor.UI.Model.Implementation.Projects;

public class Project : IProject
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
    public DateTime EndDate { get; }
    public int TotalInvestors { get; }
    public IAmountUI TotalRaised { get; }

    public override string ToString()
    {
        return Name;
    }
}