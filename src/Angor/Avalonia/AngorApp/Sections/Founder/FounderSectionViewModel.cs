using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Sections.Founder;

public partial class FounderSectionViewModel : ReactiveObject, IFounderSectionViewModel
{
    public FounderSectionViewModel(UIServices uiServices, IProjectAppService projectAppService, IInvestmentAppService investmentAppService)
    {
        this.GetPendingInvestments = ReactiveCommand.CreateFromTask(() =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybeWallet => maybeWallet
                    .ToResult("Please, create a wallet first")
                    .Bind(wallet => investmentAppService.GetPendingInvestments(wallet.Id.Value, new ProjectId("angor1qatlv9htzte8vtddgyxpgt78ruyzaj57n4l7k46"))));
        });
        
        GetPendingInvestments.HandleErrorsWith(uiServices.NotificationService, "Failed to get pending investments");
        pendingHelper = GetPendingInvestments.Successes().ToProperty(this, model => model.Pending);
        
        this.GetProjects = ReactiveCommand.CreateFromTask(() =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybeWallet => maybeWallet
                    .ToResult("Please, create a wallet first")
                    .Bind(wallet => projectAppService.GetFounderProjects(wallet.Id.Value)));
        });
        
        GetProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to get pending investments");
        projectsHelper = GetProjects.Successes().ToProperty(this, model => model.Projects);
    }

    public ReactiveCommand<Unit, Result<IEnumerable<ProjectDto>>> GetProjects { get; }

    public ReactiveCommand<Unit, Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> GetPendingInvestments { get; }

    [ObservableAsProperty]
    private IEnumerable<GetPendingInvestments.PendingInvestmentDto> pending;
    
    [ObservableAsProperty]
    private IEnumerable<ProjectDto> projects;
}