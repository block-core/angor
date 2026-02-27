using AngorApp.UI.Sections.Shared;
using AngorApp.UI.Sections.Shared.Project;

namespace AngorApp.UI.Sections.MyProjects.Items;

public class MyProjectItemSample : IMyProjectItem
{
    public IProject Project { get; set; } = new InvestmentProjectSample();
    public IEnhancedCommand ManageFunds { get; } = EnhancedCommand.Create(() => { });
}
