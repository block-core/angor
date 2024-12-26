using AngorApp.Model;

namespace AngorApp.Sections.Browse;

public class StageDesign : IStage
{
    public DateOnly ReleaseDate { get; set; }
    public uint Amount { get; set; }
    public int Index { get; set; }
    public double Weight { get; set; }
}