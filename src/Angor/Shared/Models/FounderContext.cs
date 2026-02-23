namespace Angor.Shared.Models;

/// <summary>
/// Contains all the requisite information for a founder.
/// </summary>
public class FounderContext
{
    public ProjectInfo ProjectInfo { get; set; } = null!;
    public ProjectSeeders ProjectSeeders { get; set; } = null!;
    public string ChangeAddress { get; set; } = string.Empty;
    public List<string> InvestmentTrasnactionsHex { get; set; } = new ();
}