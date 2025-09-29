using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using AngorApp.Sections.Founder.CreateProject;
using AngorApp.Sections.Founder.CreateProject.ProjectCreated;
using AngorApp.UI.Controls.Common.Success;
using AngorApp.UI.Services;
using DynamicData;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.Sections.Founder;

public class FounderSectionViewModel : ReactiveObject, IFounderSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    private readonly IProjectAppService projectAppService;
    private readonly UIServices uiServices;
    private readonly INetworkStorage networkStorage;

    public FounderSectionViewModel(UIServices uiServices, IInvestmentAppService investmentAppService, IProjectAppService projectAppService, INavigator navigator, INetworkStorage networkStorage)
    {
        this.uiServices = uiServices;
        this.projectAppService = projectAppService;
        this.networkStorage = networkStorage;

        LoadProjects = ReactiveCommand.CreateFromObservable(() => Projects(uiServices, projectAppService)).Enhance().DisposeWith(disposable);
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to get investments").DisposeWith(disposable);
        LoadProjects.Successes()
            .EditDiff(dto => dto.Id)
            .Transform(dto => new FounderProjectViewModel(navigator, dto, investmentAppService, projectAppService, uiServices))
            .Bind(out var projectList)
            .Subscribe()
            .DisposeWith(disposable);

        Create = ReactiveCommand.CreateFromTask(() =>
        {
            return uiServices.WalletRoot.TryDefaultWalletAndActivate()
                .Map(CreateWizard)
                .Map(wizard => wizard.Navigate(navigator));
        }).Enhance().DisposeWith(disposable);

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

    private ISlimWizard<string> CreateWizard(IWallet wallet)
    {
        var wizard = WizardBuilder
            .StartWith(() => new CreateProjectViewModel(wallet, uiServices, projectAppService), "Create Project").ProceedWith(model => model.Create)
            .Then(transactionId => new ProjectCreatedViewModel(transactionId, uiServices, networkStorage), "Success").ProceedWith((_, projectId) => ReactiveCommand.Create(() => Result.Success(projectId)).Enhance("Close"))
            .WithCompletionFinalStep();

        return wizard;
    }

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