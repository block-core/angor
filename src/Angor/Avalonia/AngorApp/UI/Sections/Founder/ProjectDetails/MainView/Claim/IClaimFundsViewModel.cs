using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Claim;

public interface IClaimFundsViewModel
{
    public IEnumerable<IClaimableStage> ClaimableStages { get; }
    IEnhancedCommand<Result<IEnumerable<IClaimableStage>>> LoadClaimableStages { get; }
}