using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Stage;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim;

public interface IClaimViewModel
{
    IInvestmentProject Project { get; }
    IEnumerable<IClaimStage> Stages { get; }
    IEnhancedCommand<Result<IEnumerable<IClaimStage>>> Load { get; }
}
