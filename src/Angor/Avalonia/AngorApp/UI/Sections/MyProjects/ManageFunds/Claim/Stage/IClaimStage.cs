using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Transactions;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Stage
{
    public interface IClaimStage
    {
        ICollection<IClaimableTransaction> Transactions { get; }
        IAmountUI ClaimableAmount { get; }
        IAmountUI TargetAmount { get; }
        DateTimeOffset ClaimableOn => DateTimeOffset.Now;
        IEnhancedCommand Claim { get; }
        FundsAvailability FundsAvailability { get; }
        int StageId { get; }
    }
}
