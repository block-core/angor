using System.Windows.Input;

namespace AngorApp.Sections.Browse.Details;

public interface IProjectDetailsViewModel
{
    string Name { get; }
    string ShortDescription { get; }
    object Icon { get; }
    object Picture { get; }
    public IEnumerable<Stage> Stages { get; }
    public ICommand Invest { get; }
    public string NpubKey { get; }
    public string NpubKeyHex { get; }
    public IEnumerable<INostrRelay> Relays { get; }
    public double TotalDays { get; }
    public double TotalInvestment { get; }
    public double CurrentDays { get; }
    public double CurrentInvestment { get; }
}

public class NostrRelay
{
    public Uri Uri { get; }
}

public class Stage
{
    public int Index { get; set; }
    public double Weight { get; set; }
    public DateTimeOffset ReleaseDate { get; set; }
    public decimal Amount { get; set; }
}