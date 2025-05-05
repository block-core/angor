using Angor.UI.Model;

namespace AngorApp.Sections.Browse;

public class StageDesign : IStage
{
    public DateTime ReleaseDate { get; set; }
    public long Amount { get; set; }
    public int Index { get; set; }
    public double RatioOfTotal { get; set; }
}