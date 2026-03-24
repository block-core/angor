using AngorApp.Model.ProjectsV2.InvestmentProject;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Release
{
    public interface IReleaseViewModel
    {
        public IInvestmentProject Project { get; }
        public IEnhancedCommand ReleaseAll { get; }
        public IObservable<bool> HasReleasableTransactions { get; }
    }
}
