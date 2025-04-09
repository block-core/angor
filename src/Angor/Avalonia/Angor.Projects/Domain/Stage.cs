namespace Angor.Projects.Domain;

public class Stage
{
    public DateOnly ReleaseDate { get; set; }
    public long Amount { get; set; }
    public int Index { get; set; }
    public double Weight { get; set; }
}