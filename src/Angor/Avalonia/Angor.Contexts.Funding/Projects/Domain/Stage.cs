namespace Angor.Contexts.Funding.Projects.Domain;

public class Stage
{
    public DateTime ReleaseDate { get; set; }
    public long Amount { get; set; }
    public int Index { get; set; }
    public decimal RatioOfTotal { get; set; }
}