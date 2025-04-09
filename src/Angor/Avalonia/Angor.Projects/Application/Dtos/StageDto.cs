namespace Angor.Projects.Application.Dtos;

public class StageDto
{
    public DateOnly ReleaseDate { get; set; }
    public long Amount { get; set; }
    public int Index { get; set; }
    public double Weight { get; set; }
}