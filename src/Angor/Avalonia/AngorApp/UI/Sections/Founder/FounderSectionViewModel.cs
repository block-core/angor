using System.Reactive.Disposables;
using Angor.Sdk.Funding.Projects.Application.Dtos;
using Angor.Sdk.Funding.Projects.Infrastructure.Interfaces;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Founder;

[Section("My Projects", icon: "fa-regular fa-file-lines", sortIndex: 4)]
[SectionGroup("FOUNDER")]
public class FounderSectionViewModel : ReactiveObject, IFounderSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    private readonly RefreshableCollection<IFounderProjectViewModel, ProjectId> projectsCollection;

    public FounderSectionViewModel(
        UIServices uiServices,
        IProjectAppService projectAppService,
        ICreateProjectFlow createProjectFlow,
        IWalletContext walletContext,
        Func<ProjectDto, IFounderProjectViewModel> projectViewModelFactory)
    {
        projectsCollection = RefreshableCollection.Create(
                () => walletContext.RequiresWallet(wallet => projectAppService
                    .GetFounderProjects(wallet.Id)
                    .MapEach(projectViewModelFactory)),
                project => project.Id)
            .DisposeWith(disposable);

        LoadProjects = projectsCollection.Refresh;
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to get investments").DisposeWith(disposable);

        ProjectsList = projectsCollection.Items;

        Create = ReactiveCommand.CreateFromTask(() => createProjectFlow.CreateProject()).Enhance().DisposeWith(disposable);
        Create.HandleErrorsWith(uiServices.NotificationService, "Cannot create project").DisposeWith(disposable);
    }

    public void Dispose()
    {
        disposable.Dispose();
    }

    public IReadOnlyCollection<IFounderProjectViewModel> ProjectsList { get; }

    public IEnhancedCommand<Result<IEnumerable<IFounderProjectViewModel>>> LoadProjects { get; }

    public IEnhancedCommand<Result<Maybe<string>>> Create { get; }
}
