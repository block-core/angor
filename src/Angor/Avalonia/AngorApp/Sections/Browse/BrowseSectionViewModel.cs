using System.Linq;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Wallet.Application;
using AngorApp.Features.Invest;
using AngorApp.Sections.Browse.ProjectLookup;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Mixins;
using Zafiro.Reactive;
using Zafiro.UI;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Browse;

public partial class BrowseSectionViewModel : ReactiveObject, IBrowseSectionViewModel
{
    [Reactive] private string? projectId;

    [ObservableAsProperty] private IEnumerable<IProjectViewModel>? projects;

    public BrowseSectionViewModel(IWalletAppService walletAppService, 
        IProjectAppService projectService, INavigator navigator,
        InvestWizard investWizard,
        UIServices uiServices)
    {
        ProjectLookupViewModel = new ProjectLookupViewModel(projectService, walletAppService, navigator, investWizard, uiServices);

        LoadLatestProjects = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(projectService.Latest)
            .Map(list => list.Select(dto => dto.ToProject()))
            .Map(list => list.Select(project => new ProjectViewModel(walletAppService, project, navigator, uiServices, investWizard))));

        LoadLatestProjects.HandleErrorsWith(uiServices.NotificationService, "Could not load projects");

        OpenHub = ReactiveCommand.CreateFromTask(() =>
            uiServices.LauncherService.LaunchUri(new Uri("https://www.angor.io")));
        projectsHelper = LoadLatestProjects.Successes().ToProperty(this, x => x.Projects);
        IsLoading = LoadLatestProjects.IsExecuting;
        LoadLatestProjects.Execute().Subscribe();
    }

    public IObservable<bool> IsLoading { get; }

    public IProjectLookupViewModel ProjectLookupViewModel { get; }

    public ReactiveCommand<Unit, Result<IEnumerable<ProjectViewModel>>> LoadLatestProjects { get; }

    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}