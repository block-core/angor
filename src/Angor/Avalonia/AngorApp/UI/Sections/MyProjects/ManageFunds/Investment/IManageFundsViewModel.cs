using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Release;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment
{
    public interface IManageFundsViewModel
    {
        IInvestmentProject Project { get; }
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; }
        public IObservable<IClaimViewModel> ClaimViewModel { get; }
    }
}
