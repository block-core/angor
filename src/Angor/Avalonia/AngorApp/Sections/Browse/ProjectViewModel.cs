using AngorApp.Features.Invest;
using AngorApp.Sections.Browse.Details;
using AngorApp.UI.Services;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Browse;

public class ProjectViewModel(
    IProject project,
    INavigator navigator,
    UIServices uiServices,
    InvestWizard investWizard)
    : ReactiveObject, IProjectViewModel
{
    public IProject Project { get; } = project;

    public IEnhancedCommand GoToDetails { get; set; } = ReactiveCommand.CreateFromTask(() => navigator.Go(() => new ProjectDetailsViewModel(project, investWizard, uiServices))).Enhance();
}