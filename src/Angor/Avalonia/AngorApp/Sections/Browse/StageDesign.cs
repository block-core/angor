using AngorApp.Model;

namespace AngorApp.Sections.Browse;

public class StageDesign : IStage
{
    public DateOnly ReleaseDate { get; set; }
    public decimal Amount { get; set; }
    public int Index { get; set; }
    public double Weight { get; set; }
}