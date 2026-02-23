using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Projects.Domain;

namespace Angor.Sdk.Funding.Investor;

public class InvestedProjectDto
{
    public string Id { get; set; } = string.Empty;
    public FounderStatus FounderStatus { get; set; }
    public Uri LogoUri { get; set; } = null!;
    public Amount Target { get; set; } = null!;
    
    public Amount Investment { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public Amount Raised { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public Amount InRecovery { get; set; } = null!;
    public InvestmentStatus InvestmentStatus { get; set; }
    public string InvestmentId { get; set; } = string.Empty;
}

public enum FounderStatus
{
    Invalid,
    Requested,
    Approved
}