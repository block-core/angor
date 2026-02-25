using AngorApp.UI.Shared.Samples;
using AngorApp.UI.Sections.MyProjects.ManageFunds;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release
{
    public class ReleaseViewModelSample() : IReleaseViewModel
    {
        public IManageFundsProject Project { get; } = ManageFundsProject.From(new FullProjectSample());
        public IEnhancedCommand ReleaseAll { get; }
        public bool HasReleasableTransactions => true;
    }
}
