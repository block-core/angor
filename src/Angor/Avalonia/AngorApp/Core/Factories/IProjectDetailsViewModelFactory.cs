using Angor.UI.Model.Implementation.Projects;
using AngorApp.Sections.Browse.Details;

namespace AngorApp.Core.Factories;

public interface IProjectDetailsViewModelFactory
{
    ProjectDetailsViewModel Create(FullProject project);
}

