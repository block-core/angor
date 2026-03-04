using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Fund.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Fund.Release;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Fund
{
    public interface IManageFundsViewModel
    {
        IFundProject Project { get; }
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; }
        public IObservable<IClaimViewModel> ClaimViewModel { get; }
    }
}
