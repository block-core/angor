using Angor.Contexts.Funding.Projects.Application.Dtos;

namespace AngorApp.Sections.Founder;

public interface IFounderProjectViewModelFactory
{
    IFounderProjectViewModel Create(ProjectDto project);
}

