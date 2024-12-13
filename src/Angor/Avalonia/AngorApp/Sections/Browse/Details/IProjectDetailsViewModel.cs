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
}

public class Stage
{
    public int Index { get; set; }
    public double Weight { get; set; }
    public DateTimeOffset ReleaseDate { get; set; }
    public decimal Amount { get; set; }
}