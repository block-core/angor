using Angor.Contexts.Funding.Projects.Application.Dtos;
using AngorApp.Sections.Founder.ProjectDetails;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Founder;

public class FounderProjectViewModelFactory : IFounderProjectViewModelFactory
{
    private readonly INavigator navigator;
    private readonly IFounderProjectDetailsViewModelFactory detailsFactory;

    public FounderProjectViewModelFactory(INavigator navigator, IFounderProjectDetailsViewModelFactory detailsFactory)
    {
        this.navigator = navigator;
        this.detailsFactory = detailsFactory;
    }

    public IFounderProjectViewModel Create(ProjectDto project)
    {
        return new FounderProjectViewModel(project, navigator, detailsFactory);
    }
}

