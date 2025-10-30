using AngorApp.Model.Domain.Projects;
using AngorApp.Sections.Browse;

namespace AngorApp.Core.Factories;

public interface IProjectViewModelFactory
{
    IProjectViewModel Create(IProject project);
}
