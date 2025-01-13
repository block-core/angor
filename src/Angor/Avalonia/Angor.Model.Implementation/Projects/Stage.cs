using AngorApp.Model;

namespace Angor.Model.Implementation.Projects;

public class Stage : IStage
{
    public DateOnly ReleaseDate { get; set; }
    public uint Amount { get; set; }
    public int Index { get; set; }
    public double Weight { get; set; }
}