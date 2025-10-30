using AngorApp.Model.Domain.Projects;
using AngorApp.Sections.Browse.Details;

namespace AngorApp.Core.Factories;

public interface IProjectDetailsViewModelFactory
{
    ProjectDetailsViewModel Create(FullProject project);
}

