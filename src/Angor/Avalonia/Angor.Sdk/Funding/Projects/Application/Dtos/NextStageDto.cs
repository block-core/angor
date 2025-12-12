namespace Angor.Sdk.Funding.Projects.Application.Dtos;

public class NextStageDto
{
    public int StageIndex { get; set; }
    public DateTime ReleaseDate { get; set; }
    public int DaysUntilRelease { get; set; }
    public decimal PercentageToRelease { get; set; }
}