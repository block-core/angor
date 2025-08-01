using System.Reactive.Disposables;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.Sections.Founder.CreateProject;
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

    public FounderSectionViewModel(UIServices uiServices, IProjectAppService projectAppService, Func<ProjectDto, IFounderProjectViewModel> projectViewModelFactory, INavigator navigator)
    {
        this.uiServices = uiServices;
        this.projectAppService = projectAppService;

        LoadProjects = ReactiveCommand.CreateFromObservable(() => Projects(uiServices, projectAppService)).Enhance().DisposeWith(disposable);
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to get investments").DisposeWith(disposable);
        LoadProjects.Successes()
            .EditDiff(dto => dto.Id)
            .Transform(dto => projectViewModelFactory(dto))
            .Bind(out var projectList)
            .Subscribe()
            .DisposeWith(disposable);

        Create = ReactiveCommand.CreateFromTask(() =>
        {
            return uiServices.WalletRoot.TryDefaultWalletAndActivate()
                .Map(CreateWizard)
                .Map(wizard => wizard.Navigate(navigator));
        }).Enhance();

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
            .Then(_ => new SuccessViewModel("Your project has been created successfully!"), "Success").ProceedWith((_, projectId) => ReactiveCommand.Create(() => Result.Success(projectId)).Enhance("Close"))
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