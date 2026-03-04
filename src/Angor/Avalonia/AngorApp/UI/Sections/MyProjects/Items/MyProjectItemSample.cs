using AngorApp.Model.Funded.Investment.Samples;
using AngorApp.Model.ProjectsV2;

namespace AngorApp.UI.Sections.MyProjects.Items;

public class MyProjectItemSample : IMyProjectItem
{
    public IProject Project { get; set; } = new InvestmentProjectSample();
    public IEnhancedCommand ManageFunds { get; } = EnhancedCommand.Create(() => { });
}
