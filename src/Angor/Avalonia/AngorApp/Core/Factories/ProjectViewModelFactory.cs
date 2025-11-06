using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Browse.Details;
using Zafiro.UI.Navigation;

namespace AngorApp.Core.Factories;

public class ProjectViewModelFactory : IProjectViewModelFactory
{
    private readonly IProjectAppService projectAppService;
    private readonly INavigator navigator;
    private readonly UIServices uiServices;
    private readonly Func<FullProject, IProjectDetailsViewModel> projectDetailsViewModelFactory;

    public ProjectViewModelFactory(
        IProjectAppService projectAppService,
        INavigator navigator,
        UIServices uiServices,
        Func<FullProject, IProjectDetailsViewModel> projectDetailsViewModelFactory)
    {
        this.projectAppService = projectAppService;
        this.navigator = navigator;
        this.uiServices = uiServices;
        this.projectDetailsViewModelFactory = projectDetailsViewModelFactory;
    }

    public IProjectViewModel Create(IProject project)
    {
        var goToDetails = ReactiveCommand.CreateFromTask(async () =>
            {
                var fullProject = await projectAppService.GetFullProject(new ProjectId(project.Id))
                    .Map(p => projectDetailsViewModelFactory((FullProject)p));

                var result = await fullProject.Bind(details => navigator.Go(() => details));
                return result;
            }).Enhance();

        goToDetails.HandleErrorsWith(uiServices.NotificationService, "Could not load project details");

        return new ProjectViewModel(project, goToDetails);
    }
}
