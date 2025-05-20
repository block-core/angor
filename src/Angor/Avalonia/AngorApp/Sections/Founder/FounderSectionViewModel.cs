using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.UI.Services;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Angor.Contexts.Funding.Founder.Operations;

namespace AngorApp.Sections.Founder;

public class FounderSectionViewModel : ReactiveObject, IFounderSectionViewModel
{
    public FounderSectionViewModel(UIServices uiServices, IProjectAppService projectAppService, Func<ProjectDto, IFounderProjectViewModel> projectViewModelFactory)
    {
        LoadProjects = ReactiveCommand.CreateFromObservable(() => Func(uiServices, projectAppService)).Enhance();
        
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to get investments");
        LoadProjects.Successes()
            .EditDiff(dto => dto.Id)
            .Transform(projectViewModelFactory)
            .Bind(out var projectList)
            .Subscribe();
        
        Projects = projectList;
    }

    private static IObservable<Result<IEnumerable<ProjectDto>>> Func(UIServices uiServices, IProjectAppService projectAppService)
    {
        return Observable.FromAsync(() =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybeWallet => maybeWallet
                    .ToResult("Please, create a wallet first")
                    .Bind(wallet => projectAppService.GetFounderProjects(wallet.Id.Value)));
        });
    }

    public IEnumerable<IFounderProjectViewModel> Projects { get; }
    
    public IEnhancedCommand<Unit, Result<IEnumerable<ProjectDto>>> LoadProjects { get; }
}