using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Wallet.Application;
using AngorApp.Features.Invest;
using AngorApp.Sections.Browse.Details;
using AngorApp.Sections.Browse.ProjectLookup;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Controls.Navigation;
using Zafiro.Reactive;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Browse;

public partial class BrowseSectionViewModel : ReactiveObject, IBrowseSectionViewModel
{
    [Reactive] private string? projectId;

    [ObservableAsProperty] private IList<IProjectViewModel>? projects;

    public BrowseSectionViewModel(IWalletAppService walletAppService, 
        IProjectAppService projectService, INavigator navigator,
        InvestWizard investWizard,
        UIServices uiServices)
    {
        ProjectLookupViewModel = new ProjectLookupViewModel(projectService, walletAppService, navigator, investWizard, uiServices);

        LoadLatestProjects = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(projectService.Latest)
            .Flatten()
            .Select(dto => dto.ToProject())
            .Select(IProjectViewModel (project) => new ProjectViewModel(walletAppService, project, navigator, uiServices, investWizard))
            .ToList());

        OpenHub = ReactiveCommand.CreateFromTask(() =>
            uiServices.LauncherService.LaunchUri(new Uri("https://www.angor.io")));
        projectsHelper = LoadLatestProjects.ToProperty(this, x => x.Projects);
        IsLoading = LoadLatestProjects.IsExecuting;
        LoadLatestProjects.Execute().Subscribe();
    }

    public IObservable<bool> IsLoading { get; }

    public IProjectLookupViewModel ProjectLookupViewModel { get; }

    public ReactiveCommand<Unit, IList<IProjectViewModel>> LoadLatestProjects { get; }

    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}