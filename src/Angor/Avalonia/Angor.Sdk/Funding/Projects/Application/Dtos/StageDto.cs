namespace Angor.Sdk.Funding.Projects.Application.Dtos;

public class StageDto
{
    public DateTime ReleaseDate { get; set; }
    public long Amount { get; set; }
    public int Index { get; set; }
    public decimal RatioOfTotal { get; set; }
}