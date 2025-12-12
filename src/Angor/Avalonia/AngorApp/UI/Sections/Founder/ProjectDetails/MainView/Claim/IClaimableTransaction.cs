using Angor.Sdk.Funding.Founder.Dtos;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Claim;

public interface IClaimableTransaction
{
    public IAmountUI Amount { get; }
    public string Address { get; }
    public ClaimStatus ClaimStatus { get; }
    public bool IsClaimable => ClaimStatus == ClaimStatus.Unspent;
}