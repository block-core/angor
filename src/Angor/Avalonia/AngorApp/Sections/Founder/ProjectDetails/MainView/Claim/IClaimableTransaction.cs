using Angor.Contexts.Funding.Founder.Dtos;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Claim;

public interface IClaimableTransaction
{
    public IAmountUI Amount { get; }
    public string Address { get; }
    public ClaimStatus ClaimStatus { get; }
    public bool IsClaimable => ClaimStatus == ClaimStatus.Unspent;
}