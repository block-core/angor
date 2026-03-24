using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Stage;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Fund.Claim;

public interface IClaimViewModel
{
    IFundProject Project { get; }
    IEnumerable<IClaimStage> Stages { get; }
    IEnhancedCommand<Result<IEnumerable<IClaimStage>>> Load { get; }
}
