namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Header;

public class HeaderViewModelSample : IHeaderViewModel
{
    public string ProjectTitle { get; } = "Sample Project";
    public IEnhancedCommand Refresh { get; set; } = EnhancedCommand.Create(() => { });
}
