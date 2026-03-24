using AngorApp.Model.ProjectsV2.FundProject;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Fund.Release
{
    public interface IReleaseViewModel
    {
        public IFundProject Project { get; }
        public IEnhancedCommand ReleaseAll { get; }
        public IObservable<bool> HasReleasableTransactions { get; }
    }
}
