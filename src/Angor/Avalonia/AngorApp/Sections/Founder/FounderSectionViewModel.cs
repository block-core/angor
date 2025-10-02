using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using AngorApp.UI.Controls.Common.Success;
using AngorApp.UI.Services;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Founder;

public class FounderSectionViewModel : ReactiveObject, IFounderSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    public FounderSectionViewModel(UIServices uiServices, IInvestmentAppService investmentAppService, IProjectAppService projectAppService, INavigator navigator, INetworkStorage networkStorage, ICreateProjectFlow createProjectFlow)
    {
        LoadProjects = ReactiveCommand.CreateFromObservable(() => Projects(uiServices, projectAppService)).Enhance().DisposeWith(disposable);
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to get investments").DisposeWith(disposable);
        LoadProjects.Successes()
            .EditDiff(dto => dto.Id)
            .Transform(dto => new FounderProjectViewModel(navigator, dto, investmentAppService, projectAppService, uiServices))
            .Bind(out var projectList)
            .Subscribe()
            .DisposeWith(disposable);

        Create = ReactiveCommand.CreateFromTask(() => createProjectFlow.CreateProject()).Enhance().DisposeWith(disposable);
        Create.HandleErrorsWith(uiServices.NotificationService, "Cannot create project").DisposeWith(disposable);

        ProjectsList = projectList;
    }
    
    public void Dispose()
    {
        disposable.Dispose();
        LoadProjects.Dispose();
    }

    public IEnumerable<IFounderProjectViewModel> ProjectsList { get; }

    public IEnhancedCommand<Unit, Result<Maybe<string>>> Create { get; }

    public IEnhancedCommand<Unit, Result<IEnumerable<ProjectDto>>> LoadProjects { get; }

    private static IObservable<Result<IEnumerable<ProjectDto>>> Projects(UIServices uiServices, IProjectAppService projectAppService)
    {
        return Observable.FromAsync(() =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybeWallet => maybeWallet.ToResult("Please, create a wallet first")
                    .Bind(wallet => projectAppService.GetFounderProjects(wallet.Id.Value)));
        });
    }
}

