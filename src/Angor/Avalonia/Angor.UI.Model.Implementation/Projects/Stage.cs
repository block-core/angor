namespace Angor.UI.Model.Implementation.Projects;

public class Stage : IStage
{
    public DateTime ReleaseDate { get; set; }
    public long Amount { get; set; }
    public int Index { get; set; }
    public decimal RatioOfTotal { get; set; }
}