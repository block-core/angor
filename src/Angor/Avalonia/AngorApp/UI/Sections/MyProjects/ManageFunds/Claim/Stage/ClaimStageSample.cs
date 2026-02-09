using Angor.Sdk.Funding.Founder.Dtos;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Transactions;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Stage
{
    public class ClaimStageSample : IClaimStage
    {
        public ICollection<IClaimableTransaction> Transactions { get; set; } = new List<IClaimableTransaction>
        {
            new ClaimableTransactionSample(), new ClaimableTransactionSample(), new ClaimableTransactionSample()
        };

        public IAmountUI ClaimableAmount { get; } = AmountUI.FromBtc(0.093824);

        public IAmountUI TargetAmount { get; } = new AmountUI(1000000);
        public IEnhancedCommand Claim { get; set; } = EnhancedCommand.Create(() => { }, text: "Claim", name: "Claim");
        public FundsAvailability FundsAvailability { get; set; }
        public int StageId { get; set; } = 1;
    }
}
