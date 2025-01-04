using System.Windows.Input;
using AngorApp.Model;

namespace AngorApp.Sections.Browse.Details;

public interface IProjectDetailsViewModel
{
    object Icon { get; }
    object Picture { get; }
    public ICommand Invest { get; }
    public IEnumerable<INostrRelay> Relays { get; }
    public double TotalDays { get; }
    public double TotalInvestment { get; }
    public double CurrentDays { get; }
    public double CurrentInvestment { get; }
    public IProject Project { get; }
}

public class Stage
{
    public int Index { get; set; }
    public double Weight { get; set; }
    public DateTimeOffset ReleaseDate { get; set; }
    public decimal Amount { get; set; }
}