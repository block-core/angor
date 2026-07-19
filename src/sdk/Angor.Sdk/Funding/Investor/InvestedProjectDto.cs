using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Shared.Models;

namespace Angor.Sdk.Funding.Investor;

public class InvestedProjectDto
{
    public string Id { get; set; }
    public FounderStatus FounderStatus { get; set; }
    public Uri LogoUri { get; set; }
    public Uri BannerUri { get; set; }
    public Amount Target { get; set; }
    
    public Amount Investment { get; set; }
    public string Name { get; set; }
    public Amount Raised { get; set; }
    public string Description { get; set; }
    public Amount InRecovery { get; set; }
    public InvestmentStatus InvestmentStatus { get; set; }
    public string InvestmentId { get; set; }
    public DateTimeOffset? RequestedOn { get; set; }

    /// <summary>
    /// On-chain confirmation date of the investment transaction (from the indexer).
    /// Falls back to the request time when the transaction isn't confirmed yet.
    /// </summary>
    public DateTimeOffset? TransactionDate { get; set; }

    /// <summary>
    /// Project funding-window start date, carried from the project info.
    /// </summary>
    public DateTime StartingDate { get; set; }

    /// <summary>
    /// Project funding-window end date, carried from the project info so the
    /// investor's "Investment End Date" can be shown.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Project type: Invest, Fund, or Subscribe. Defaults to Invest for backward compatibility.
    /// </summary>
    public ProjectType ProjectType { get; set; } = ProjectType.Invest;

    /// <summary>
    /// Total number of unique investors in this project (from indexer stats).
    /// </summary>
    public int TotalInvestors { get; set; }

    /// <summary>
    /// This investor's share of the total project investment as a percentage (0-100).
    /// For Fund projects, this is "as of now" since funds can always be added.
    /// </summary>
    public double SharePercentage { get; set; }

    /// <summary>
    /// Amount from this investor's share that has been claimed/spent by the founder (in sats).
    /// </summary>
    public long AmountClaimedByFounder { get; set; }

    /// <summary>
    /// Percentage of this investor's total that has been claimed by the founder (0-100).
    /// </summary>
    public double ClaimedPercentage { get; set; }
}

public enum FounderStatus
{
    Invalid,
    Requested,
    Approved
}
