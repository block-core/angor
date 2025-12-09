namespace Angor.Contexts.Funding.Projects.Application.Dtos;

public class DynamicStageDto
{
    public int StageIndex { get; set; }
    public DateTime ReleaseDate { get; set; }
    public long TotalAmount { get; set; }
    public int TransactionCount { get; set; }
    public int UnspentTransactionCount { get; set; }
    public long UnspentAmount { get; set; }
    public bool IsReleased => ReleaseDate <= DateTime.UtcNow;
}
