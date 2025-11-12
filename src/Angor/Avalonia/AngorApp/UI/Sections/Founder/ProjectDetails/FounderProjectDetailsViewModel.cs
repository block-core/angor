using System.Reactive.Disposables;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.Model.Projects;
using AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Approve;
using AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Claim;
using AngorApp.UI.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;
using Zafiro.CSharpFunctionalExtensions;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Founder.ProjectDetails;

public partial class FounderProjectDetailsViewModel : ReactiveObject, IFounderProjectDetailsViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    [ObservableAsProperty] private IFullProject? project;
    [ObservableAsProperty] private object? contentViewModel;

    public FounderProjectDetailsViewModel(ProjectId projectId, IProjectAppService projectAppService, IFounderAppService founderAppService, UIServices uiServices, IWalletContext walletContext)
    {
        var loadProject = EnhancedCommand.Create(() => projectAppService.GetFullProject(projectId)).DisposeWith(disposable);
        loadProject.HandleErrorsWith(uiServices.NotificationService);

        var projectStream = loadProject.Successes().Publish().RefCount();

        projectHelper = projectStream.ToProperty(this, x => x.Project).DisposeWith(disposable);
        contentViewModelHelper = projectStream
            .Select(p => CreateContent(p.ProjectId, p.Status, founderAppService, uiServices, walletContext))
            .ToProperty(this, x => x.ContentViewModel)
            .DisposeWith(disposable);

        Load = loadProject;

        loadProject.Execute().Subscribe().DisposeWith(disposable);
    }

    private static object CreateContent(ProjectId projectId, ProjectStatus status, IFounderAppService founderAppService, UIServices uiServices, IWalletContext walletContext)
    {
        return status switch
        {
            ProjectStatus.Failed => new ReleaseFundsViewModel(projectId, founderAppService, walletContext, uiServices),
            ProjectStatus.Succeeded => new ClaimFundsViewModel(projectId, founderAppService, uiServices, walletContext),
            ProjectStatus.Started => new ApproveInvestmentsViewModel(projectId, founderAppService, uiServices, walletContext),
            ProjectStatus.Funding => new ApproveInvestmentsViewModel(projectId, founderAppService, uiServices, walletContext),
            _ => new object()
        };
    }

    public IEnhancedCommand<Result<IFullProject>> Load { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
