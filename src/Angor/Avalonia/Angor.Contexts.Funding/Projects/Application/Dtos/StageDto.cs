namespace Angor.Contexts.Funding.Projects.Application.Dtos;

public class StageDto
{
    public DateTime ReleaseDate { get; set; }
    public long Amount { get; set; }
    public int Index { get; set; }
    public double RatioOfTotal { get; set; }
}