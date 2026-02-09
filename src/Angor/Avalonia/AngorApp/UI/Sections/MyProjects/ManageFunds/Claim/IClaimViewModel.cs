using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Stage;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim;

public interface IClaimViewModel
{
    IFullProject Project { get; }
    IEnumerable<IClaimStage> Stages { get; }
    IEnhancedCommand<Result<IEnumerable<IClaimStage>>> Load { get; }
}