using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Transactions;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Stage
{
    public interface IClaimStage
    {
        ICollection<ITransaction> Transactions { get; }
        IAmountUI ClaimableAmount { get; }
        IAmountUI TargetAmount { get; }
        DateTimeOffset ClaimableOn => DateTimeOffset.Now;
        IEnhancedCommand Claim { get; }
        FundsAvailability FundsAvailability { get; }
        int StageId { get; }
    }
}
