using AngorApp.UI.Sections.Browse;

namespace AngorApp.Core.Factories;

public interface IProjectViewModelFactory
{
    IProjectViewModel Create(IProject project);
}
