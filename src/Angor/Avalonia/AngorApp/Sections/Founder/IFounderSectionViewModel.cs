using Angor.Contexts.Funding.Projects.Application.Dtos;

namespace AngorApp.Sections.Founder;

public interface IFounderSectionViewModel
{
    ReactiveCommand<Unit, Result<IEnumerable<ProjectDto>>> LoadProjects { get; }
    public IEnumerable<IFounderProjectViewModel> Projects { get; }
}