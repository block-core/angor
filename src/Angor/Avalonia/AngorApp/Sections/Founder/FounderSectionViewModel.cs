using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.UI.Services;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Sections.Founder;

public partial class FounderSectionViewModel : ReactiveObject, IFounderSectionViewModel
{
    public FounderSectionViewModel(UIServices uiServices, IProjectAppService projectAppService, Func<ProjectDto, IFounderProjectViewModel> projectViewModelFactory)
    {
        LoadProjects = ReactiveCommand.CreateFromTask(() =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybeWallet => maybeWallet
                    .ToResult("Please, create a wallet first")
                    .Bind(wallet => projectAppService.GetFounderProjects(wallet.Id.Value)));
        });
        
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to get pending investments");
        LoadProjects.Successes().EditDiff(dto => dto.Id)
            .Transform(projectViewModelFactory)
            .Bind(out var projectList)
            .Subscribe();
        
        Projects = projectList;
    }

    public IEnumerable<IFounderProjectViewModel> Projects { get; }
    
    public ReactiveCommand<Unit, Result<IEnumerable<ProjectDto>>> LoadProjects { get; }
}