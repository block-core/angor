using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Release;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds
{
    public interface IManageFundsViewModel
    {
        IManageFundsProject Project { get; }
        IObservable<IManageFundsProject> ProjectObs { get; }
        IEnhancedCommand Load { get; }
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; }
        public IObservable<IClaimViewModel> ClaimViewModel { get; }
    }
}
