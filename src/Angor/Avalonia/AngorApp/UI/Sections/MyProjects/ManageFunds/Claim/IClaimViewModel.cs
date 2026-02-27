using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Stage;
using AngorApp.UI.Sections.MyProjects.ManageFunds;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim;

public interface IClaimViewModel
{
    IManageFundsProject Project { get; }
    IEnumerable<IClaimStage> Stages { get; }
    IEnhancedCommand<Result<IEnumerable<IClaimStage>>> Load { get; }
}
