using AngorApp.UI.Sections.MyProjects.ManageFunds;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release
{
    public interface IReleaseViewModel
    {
        public IManageFundsProject Project { get; }
        public IEnhancedCommand ReleaseAll { get; }
        public bool HasReleasableTransactions { get; }
    }
}
