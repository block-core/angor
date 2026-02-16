namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Header;

public interface IHeaderViewModel
{
    public string ProjectTitle { get; }
    public IEnhancedCommand<Result<IFullProject>> Refresh { get; }
}