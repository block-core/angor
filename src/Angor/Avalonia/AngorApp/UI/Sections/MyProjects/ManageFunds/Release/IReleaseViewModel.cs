namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release
{
    public interface IReleaseViewModel
    {
        public IFullProject Project { get; }
        public IEnhancedCommand ReleaseAll { get; }
        public bool HasReleasableTransactions { get; }
    }
}