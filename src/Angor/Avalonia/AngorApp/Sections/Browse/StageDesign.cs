using Angor.UI.Model;

namespace AngorApp.Sections.Browse;

public class StageDesign : IStage
{
    public DateOnly ReleaseDate { get; set; }
    public long Amount { get; set; }
    public int Index { get; set; }
    public double Weight { get; set; }
}