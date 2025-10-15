using Angor.UI.Model.Implementation.Projects;
using AngorApp.Sections.Browse;

namespace AngorApp.Core.Factories;

public interface IProjectViewModelFactory
{
    IProjectViewModel Create(IProject project);
}
