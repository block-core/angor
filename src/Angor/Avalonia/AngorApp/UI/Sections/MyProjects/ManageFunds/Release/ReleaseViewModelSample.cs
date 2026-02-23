using AngorApp.UI.Shared.Samples;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release
{
    public class ReleaseViewModelSample() : IReleaseViewModel
    {
        public IFullProject Project { get; } = new FullProjectSample();
        public IEnhancedCommand ReleaseAll { get; } = null!;
    }
}