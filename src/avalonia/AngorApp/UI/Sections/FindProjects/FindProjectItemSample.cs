using AngorApp.Model.Funded.Investment.Samples;
using AngorApp.Model.ProjectsV2;

namespace AngorApp.UI.Sections.FindProjects
{
    public class FindProjectItemSample : IFindProjectItem
    {
        public IProject Project { get; set; } = new InvestmentProjectSample();
        public IEnhancedCommand GoToDetails { get; } = EnhancedCommand.Create(() => { });
    }
}
