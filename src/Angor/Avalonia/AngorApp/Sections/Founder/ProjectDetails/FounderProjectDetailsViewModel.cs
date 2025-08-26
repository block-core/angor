using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.Sections.Founder.ProjectDetails.MainView;
using AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails;

public partial class FounderProjectDetailsViewModel : ReactiveObject, IFounderProjectDetailsViewModel, IDisposable
{
    private readonly IProjectAppService projectAppService;

    [ObservableAsProperty]
    private IProjectMainViewModel projectMain;
    
    private readonly CompositeDisposable disposable = new();

    public FounderProjectDetailsViewModel(ProjectId projectId, IProjectAppService projectAppService, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.projectAppService = projectAppService;
        Load = ReactiveCommand.CreateFromTask(() => DoLoadFullProject(projectId).Map(project => (IProjectMainViewModel)new ProjectMainViewModel(project, investmentAppService, uiServices))).Enhance();
        Load.HandleErrorsWith(uiServices.NotificationService);

        projectMainHelper = Load.Successes().ToProperty(this, x => x.ProjectMain).DisposeWith(disposable);
        Load.Execute().Subscribe().DisposeWith(disposable);
    }

    public IEnhancedCommand<Result<IProjectMainViewModel>> Load { get; }

    private Task<Result<FullProject>> DoLoadFullProject(ProjectId projectId)
    {
        return from project in projectAppService.Get(projectId)
            from stats in projectAppService.GetProjectStatistics(projectId)
            select new FullProject(project, stats);
    }


    public void Dispose()
    {
        disposable.Dispose();
    }
}