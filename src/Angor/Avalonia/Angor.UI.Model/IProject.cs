namespace Angor.UI.Model;

public interface IProject
{
    public string Id { get; }
    public string Name { get; }
    public Uri Banner { get; }
    public Uri Picture { get; }
    public string ShortDescription { get; }
    string BitcoinAddress { get; }
    public IAmountUI TargetAmount { get; }
    public DateTime StartDate { get; }
    IEnumerable<IStage> Stages { get; }
    public string NostrNpubKeyHex { get; }
    public TimeSpan PenaltyDuration { get; }
    public Uri InformationUri { get; }
    public DateTime EndDate { get; }
    public int TotalInvestors { get; }
    public IAmountUI TotalRaised { get; }
}