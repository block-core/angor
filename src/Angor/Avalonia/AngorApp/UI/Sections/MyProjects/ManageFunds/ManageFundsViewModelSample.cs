using AngorApp.UI.Shared.Samples;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Release;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds
{
    public class ManageFundsViewModelSample : IManageFundsViewModel
    {
        public IManageFundsProject Project { get; } = ManageFundsProject.From(new FullProjectSample());
        public IObservable<IManageFundsProject> ProjectObs { get; } = Observable.Return<IManageFundsProject>(ManageFundsProject.From(new FullProjectSample()));
        public IEnhancedCommand Load { get; } = ReactiveCommand.Create(() => { }).Enhance();
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; } = Observable.Return(new ReleaseViewModelSample());
        public IObservable<IClaimViewModel> ClaimViewModel { get; } = Observable.Return(new ClaimViewModelSample());
    }
}
