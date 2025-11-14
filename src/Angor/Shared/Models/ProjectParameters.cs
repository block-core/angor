namespace Angor.Shared.Models;

/// <summary>
/// Parameters for creating a project investment transaction.
/// </summary>
public class ProjectParameters
{
    /// <summary>
    /// The investor's public key.
    /// </summary>
    public string InvestorKey { get; set; }

    /// <summary>
    /// Total amount to invest in satoshis.
    /// </summary>
    public long TotalInvestmentAmount { get; set; }

    /// <summary>
    /// The date when the investment is made.
    /// For Fund/Subscribe projects, this is used to calculate dynamic stage dates.
    /// For Invest projects, this can be null or DateTime.UtcNow.
    /// </summary>
    public DateTime? InvestmentStartDate { get; set; }

    /// <summary>
    /// Index of the dynamic stage pattern to use (0-255).
    /// Only applicable for Fund/Subscribe projects.
    /// For Invest projects, this is ignored.
    /// </summary>
    public byte PatternIndex { get; set; }

    /// <summary>
    /// Creates project parameters with default values.
    /// </summary>
    public static ProjectParameters Create(string investorKey, long totalInvestmentAmount)
    {
        return new ProjectParameters
        {
            InvestorKey = investorKey,
            TotalInvestmentAmount = totalInvestmentAmount,
            InvestmentStartDate = DateTime.UtcNow,
            PatternIndex = 0
        };
    }

    /// <summary>
    /// Creates project parameters for a Fund/Subscribe project with pattern selection.
    /// </summary>
    public static ProjectParameters CreateForDynamicProject(
        string investorKey,
        long totalInvestmentAmount,
        byte patternIndex,
        DateTime? investmentStartDate = null)
    {
        return new ProjectParameters
        {
            InvestorKey = investorKey,
            TotalInvestmentAmount = totalInvestmentAmount,
            InvestmentStartDate = investmentStartDate ?? DateTime.UtcNow,
            PatternIndex = patternIndex
        };
    }
}
