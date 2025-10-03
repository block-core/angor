using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.UI.Model.Implementation.Projects;
using AngorApp.Features.Invest;
using AngorApp.Sections.Browse.Details;
using AngorApp.UI.Services;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Browse;

public class ProjectViewModel : ReactiveObject, IProjectViewModel
{
    public ProjectViewModel(IProject project,
        IProjectAppService projectAppService,
        INavigator navigator,
        UIServices uiServices,
        InvestWizard investWizard,
        IInvestmentAppService investmentAppService)
    {
        Project = project;

        GoToDetails = ReactiveCommand.CreateFromTask(async () =>
        {
            var fullProject = await projectAppService.GetFullProject(new ProjectId(project.Id))
                .Map(fullProject => new ProjectDetailsViewModel(fullProject, investWizard, uiServices, investmentAppService, navigator));

            var result = await fullProject.Bind(details => navigator.Go(() => details));
            return result;
        }).Enhance();
        
        GoToDetails.HandleErrorsWith(uiServices.NotificationService, "Could not load project details");
    }

    public IProject Project { get; }

    public IEnhancedCommand<Result<Unit>> GoToDetails { get; set; }
}