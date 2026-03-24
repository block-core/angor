using AngorApp.Model.Funded.Fund.Samples;
using AngorApp.Model.ProjectsV2.FundProject;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Fund.Release
{
    public class ReleaseViewModelSample() : IReleaseViewModel
    {
        public IFundProject Project { get; } = new FundProjectSample();
        public IEnhancedCommand ReleaseAll { get; } = EnhancedCommand.Create(() => { });
        public IObservable<bool> HasReleasableTransactions { get; } = Observable.Return(true);
    }
}
