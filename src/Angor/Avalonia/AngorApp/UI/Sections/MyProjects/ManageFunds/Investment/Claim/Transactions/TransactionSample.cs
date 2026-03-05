using Angor.Sdk.Funding.Founder.Dtos;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Transactions
{
    public class TransactionSample : ITransaction
    {
        public IAmountUI Amount { get; set; } = new AmountUI(10000);
        public string Address { get; set; } = "sample_address";
        public int StageId { get; set; }
        public ClaimStatus ClaimStatus { get; set; } = ClaimStatus.SpentByFounder;
        public bool IsClaimable => ClaimStatus == ClaimStatus.Unspent;
    }
}
