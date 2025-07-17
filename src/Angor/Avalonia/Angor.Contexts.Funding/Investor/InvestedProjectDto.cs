using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Investor;

public class InvestedProjectDto
{
    public string Id { get; set; }
    public FounderStatus FounderStatus { get; set; }
    public Uri LogoUri { get; set; }
    public Amount Target { get; set; }
    
    public Amount Investment { get; set; }
    public string Name { get; set; }
    public Amount Raised { get; set; }
    public string Description { get; set; }
    public Amount InRecovery { get; set; }
    public InvestmentStatus InvestmentStatus { get; set; }
    public string InvestmentId { get; set; }
}

public enum FounderStatus
{
    Invalid,
    Requested,
    Approved
}