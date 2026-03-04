using Angor.Sdk.Funding.Projects.Dtos;
using AngorApp.Model.ProjectsV2;

namespace AngorApp.Core.Factories;

public interface IProjectFactory
{
    IProject Create(ProjectDto dto);
}
