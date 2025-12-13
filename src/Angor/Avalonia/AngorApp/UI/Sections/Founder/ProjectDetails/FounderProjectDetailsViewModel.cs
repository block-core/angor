using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Approve;
using AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Claim;
using AngorApp.UI.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;
using Zafiro.CSharpFunctionalExtensions;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

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
    }

    private static object CreateContent(ProjectId projectId, ProjectStatus status, IFounderAppService founderAppService, UIServices uiServices, IWalletContext walletContext)
    {
        // todo: fix this so that we can load approve
        // and claim at the same time for fund type projects
        // this is an ugly hack to bypass the static object in the method signature
        var investmentRequests = Task.Run(async () => 
        {
            var a = await founderAppService.GetInvestments(new GetInvestments.GetInvestmentsRequest(walletContext.CurrentWallet.Value.Id, projectId));
            return a;
                
        }).GetAwaiter().GetResult();

        if(investmentRequests.Value.Investments.Any(_ => _.Status == InvestmentStatus.PendingFounderSignatures))
        {
            return new ApproveInvestmentsViewModel(projectId, founderAppService, uiServices, walletContext);
        }

        var enableProductionValidations = uiServices.EnableProductionValidations();

        if (enableProductionValidations == false)
        {
            return new ClaimFundsViewModel(projectId, founderAppService, uiServices, walletContext);
        }

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
