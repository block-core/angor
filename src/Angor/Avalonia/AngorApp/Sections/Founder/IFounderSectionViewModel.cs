using Angor.Contexts.Funding.Projects.Application.Dtos;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder;

public interface IFounderSectionViewModel
{
    IEnhancedCommand<Unit, Result<IEnumerable<ProjectDto>>> LoadProjects { get; }
    public IEnumerable<IFounderProjectViewModel> Projects { get; }
}