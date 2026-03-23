using AngorApp.Model.Funded.Fund.Samples;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Fund.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Fund.Release;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Fund
{
    public class ManageFundsViewModelSample : IManageFundsViewModel
    {
        public IFundProject Project { get; } = new FundProjectSample();
        public IEnhancedCommand LoadProjectStats { get; } = ReactiveCommand.Create(() => { }).Enhance();
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; } = Observable.Return(new ReleaseViewModelSample());
        public IObservable<IClaimViewModel> ClaimViewModel { get; } = Observable.Return(new ClaimViewModelSample());
    }
}
