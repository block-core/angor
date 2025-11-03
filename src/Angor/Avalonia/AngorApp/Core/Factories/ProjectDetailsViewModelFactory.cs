using AngorApp.Model.Projects;
using AngorApp.Sections.Browse.Details;
using AngorApp.UI.Controls.Common.FoundedProjectOptions;

namespace AngorApp.Core.Factories;

public class ProjectDetailsViewModelFactory : IProjectDetailsViewModelFactory
{
    private readonly IProjectInvestCommandFactory investCommandFactory;
    private readonly IFoundedProjectOptionsViewModelFactory foundedProjectOptionsFactory;

    public ProjectDetailsViewModelFactory(
        IProjectInvestCommandFactory investCommandFactory,
        IFoundedProjectOptionsViewModelFactory foundedProjectOptionsFactory)
    {
        this.investCommandFactory = investCommandFactory;
        this.foundedProjectOptionsFactory = foundedProjectOptionsFactory;
    }

    public ProjectDetailsViewModel Create(FullProject project)
    {
        return new ProjectDetailsViewModel(project, investCommandFactory, foundedProjectOptionsFactory);
    }
}
