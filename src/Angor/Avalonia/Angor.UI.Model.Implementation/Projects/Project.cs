namespace Angor.UI.Model.Implementation.Projects;

public class Project : IProject
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Uri Picture { get; set; }
    public Uri Icon { get; set; }
    public string ShortDescription { get; set; }
    public string BitcoinAddress { get; set; }
    public decimal TargetAmount { get; set; }
    public DateOnly StartingDate { get; set; }
    public IEnumerable<IStage> Stages { get; set; }
    public string NpubKey { get; set; }
    public string NpubKeyHex { get; set; }
    public TimeSpan PenaltyDuration { get; set; }
    public Uri InformationUri { get; set; }

    public override string ToString()
    {
        return Name;
    }
}