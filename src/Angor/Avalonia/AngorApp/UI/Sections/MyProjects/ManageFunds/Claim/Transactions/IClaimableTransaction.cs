using Angor.Sdk.Funding.Founder.Dtos;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Transactions
{
    public interface IClaimableTransaction
    {
        IAmountUI Amount { get; }
        string Address { get; }
        ClaimStatus ClaimStatus { get; }
        bool IsClaimable => ClaimStatus == ClaimStatus.Unspent;
    }
}
