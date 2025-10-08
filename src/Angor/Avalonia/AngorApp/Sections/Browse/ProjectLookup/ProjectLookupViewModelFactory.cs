using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.Core.Factories;

namespace AngorApp.Sections.Browse.ProjectLookup;

public class ProjectLookupViewModelFactory : IProjectLookupViewModelFactory
{
    private readonly IProjectAppService projectAppService;
    private readonly IProjectViewModelFactory projectViewModelFactory;

    public ProjectLookupViewModelFactory(
        IProjectAppService projectAppService,
        IProjectViewModelFactory projectViewModelFactory)
    {
        this.projectAppService = projectAppService;
        this.projectViewModelFactory = projectViewModelFactory;
    }

    public IProjectLookupViewModel Create()
    {
        return new ProjectLookupViewModel(projectAppService, projectViewModelFactory);
    }
}
