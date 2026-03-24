using AngorApp.Model.Funded.Investment.Samples;
using AngorApp.Model.ProjectsV2.InvestmentProject;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Release
{
    public class ReleaseViewModelSample() : IReleaseViewModel
    {
        public IInvestmentProject Project { get; } = new InvestmentProjectSample();
        public IEnhancedCommand ReleaseAll { get; } = EnhancedCommand.Create(() => { });
        public IObservable<bool> HasReleasableTransactions { get; } = Observable.Return(true);
    }
}
