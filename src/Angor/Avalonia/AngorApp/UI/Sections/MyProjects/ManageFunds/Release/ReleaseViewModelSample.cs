using AngorApp.UI.Sections.Browse.Details;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release
{
    public class ReleaseViewModelSample() : IReleaseViewModel
    {
        public IFullProject Project { get; } = new FullProjectSample();
        public IEnhancedCommand ReleaseAll { get; }
    }
}