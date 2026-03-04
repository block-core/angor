using Angor.Sdk.Funding.Founder.Dtos;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Transactions
{
    public interface ITransaction
    {
        IAmountUI Amount { get; }
        string Address { get; }
        int StageId { get; }
        ClaimStatus ClaimStatus { get; }
        bool IsClaimable => ClaimStatus == ClaimStatus.Unspent;
    }
}
