namespace Angor.UI.Model;

public interface IProject
{
    public string Id { get; set; }
    public string Name { get; }
    public Uri Picture { get; }
    public Uri Icon { get; }
    public string ShortDescription { get; }
    string BitcoinAddress { get; }
    public decimal TargetAmount { get; }
    public DateOnly StartingDate { get; }
    IEnumerable<IStage> Stages { get; }
    public string NpubKey { get; }
    public string NpubKeyHex { get; }
    public TimeSpan PenaltyDuration { get; }
    public Uri InformationUri { get; }
}