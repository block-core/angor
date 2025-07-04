using System.Reactive.Disposables;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.UI.Services;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder;

public class FounderSectionViewModel : ReactiveObject, IFounderSectionViewModel, IDisposable
{
    private CompositeDisposable disposable = new();

    public FounderSectionViewModel(UIServices uiServices, IProjectAppService projectAppService, Func<ProjectDto, IFounderProjectViewModel> projectViewModelFactory)
    {
        LoadProjects = ReactiveCommand.CreateFromObservable(() => Projects(uiServices, projectAppService)).Enhance().DisposeWith(disposable);
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to get investments").DisposeWith(disposable);
        LoadProjects.Successes()
            .EditDiff(dto => dto.Id)
            .Transform(dto => projectViewModelFactory(dto))
            .Bind(out var projectList)
            .Subscribe()
            .DisposeWith(disposable);

        ProjectsList = projectList;
    }

    private static IObservable<Result<IEnumerable<ProjectDto>>> Projects(UIServices uiServices, IProjectAppService projectAppService)
    {
        return Observable.FromAsync(() =>
        {
            return uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybeWallet => maybeWallet
                    .ToResult("Please, create a wallet first")
                    .Bind(wallet => projectAppService.GetFounderProjects(wallet.Id.Value)));
        });
    }

    public IEnumerable<IFounderProjectViewModel> ProjectsList { get; }

    public IEnhancedCommand<Unit, Result<IEnumerable<ProjectDto>>> LoadProjects { get; }

    public void Dispose()
    {
        disposable.Dispose();
        LoadProjects.Dispose();
    }
}