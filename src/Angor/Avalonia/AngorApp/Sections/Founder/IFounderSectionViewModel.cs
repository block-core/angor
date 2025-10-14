namespace AngorApp.Sections.Founder;

public interface IFounderSectionViewModel
{
    IEnhancedCommand<Result<IEnumerable<IFounderProjectViewModel>>> LoadProjects { get; }
    IReadOnlyCollection<IFounderProjectViewModel> ProjectsList { get; }
    IEnhancedCommand<Result<Maybe<string>>> Create { get; }
}
