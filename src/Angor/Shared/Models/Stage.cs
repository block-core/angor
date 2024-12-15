namespace Angor.Shared.Models;

public class Stage
{
    public int StageNumber { get; set; } 
    public decimal AmountToRelease { get; set; } 
    public DateTime ReleaseDate { get; set; } 
    public string EventId { get; set; } 
    public bool IsPublished { get; set; }
}