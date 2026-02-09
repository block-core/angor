using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Release;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds
{
    public interface IManageFundsViewModel
    {
        IFullProject Project { get; }
        IObservable<IFullProject> ProjectObs { get; }
        IEnhancedCommand<Result<IFullProject>> Load { get; }
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; }
        public IObservable<IClaimViewModel> ClaimViewModel { get; }
    }
}