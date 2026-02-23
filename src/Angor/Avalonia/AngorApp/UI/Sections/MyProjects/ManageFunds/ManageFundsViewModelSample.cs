using AngorApp.UI.Shared.Samples;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Release;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds
{
    public class ManageFundsViewModelSample : IManageFundsViewModel
    {
        public IFullProject Project { get; } = new FullProjectSample();
        public IObservable<IFullProject> ProjectObs { get; } = null!;
        public IEnhancedCommand<Result<IFullProject>> Load { get; } = null!;
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; } = Observable.Return(new ReleaseViewModelSample());
        public IObservable<IClaimViewModel> ClaimViewModel { get; } = Observable.Return(new ClaimViewModelSample());
    }
}