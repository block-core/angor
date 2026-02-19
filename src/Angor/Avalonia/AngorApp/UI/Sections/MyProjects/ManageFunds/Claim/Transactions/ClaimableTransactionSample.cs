using Angor.Sdk.Funding.Founder.Dtos;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Transactions
{
    public class ClaimableTransactionSample : IClaimableTransaction
    {
        public IAmountUI Amount { get; set; } = new AmountUI(10000);
        public string Address { get; set; } = "sample_address";
        public ClaimStatus ClaimStatus { get; set; } = ClaimStatus.SpentByFounder;
    }
}
