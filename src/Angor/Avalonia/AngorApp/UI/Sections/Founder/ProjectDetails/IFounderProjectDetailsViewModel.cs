namespace AngorApp.UI.Sections.Founder.ProjectDetails;

public interface IFounderProjectDetailsViewModel
{
    IEnhancedCommand<Result<IFullProject>> Load { get; }
    IFullProject? Project { get; }
    object? ContentViewModel { get; }
}
