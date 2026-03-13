using AngorApp.Model.Funded.Investment.Samples;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Release;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment
{
    public class ManageFundsViewModelSample : IManageFundsViewModel
    {
        public IInvestmentProject Project { get; } = new InvestmentProjectSample();
        public IEnhancedCommand LoadProjectStats { get; } = ReactiveCommand.Create(() => { }).Enhance();
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; } = Observable.Return(new ReleaseViewModelSample());
        public IObservable<IClaimViewModel> ClaimViewModel { get; } = Observable.Return(new ClaimViewModelSample());
    }
}
