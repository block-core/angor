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
    /// Project type: Invest, Fund, or Subscribe. Defaults to Invest for backward compatibility.
    /// </summary>
    public ProjectType ProjectType { get; set; } = ProjectType.Invest;

    /// <summary>
    /// Total number of unique investors in this project (from indexer stats).
    /// </summary>
    public int TotalInvestors { get; set; }
}

public enum FounderStatus
{
    Invalid,
    Requested,
    Approved
}
