using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Transactions;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Stage
{
    public class ClaimStageSample : IClaimStage
    {
        public ICollection<ITransaction> Transactions { get; set; } = new List<ITransaction>
        {
            new TransactionSample(), new TransactionSample(), new TransactionSample()
        };

        public IAmountUI ClaimableAmount { get; } = AmountUI.FromBtc(0.093824);

        public IAmountUI TargetAmount { get; } = new AmountUI(1000000);
        public IEnhancedCommand Claim { get; set; } = EnhancedCommand.Create(() => { }, text: "Claim", name: "Claim");
        public FundsAvailability FundsAvailability { get; set; }
        public int StageId { get; set; } = 1;
    }
}
