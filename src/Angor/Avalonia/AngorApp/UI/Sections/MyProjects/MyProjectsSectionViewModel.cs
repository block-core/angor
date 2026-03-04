using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Angor.Sdk.Funding.Projects;
using AngorApp.Core.Factories;
using AngorApp.Model.Common;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.UI.Sections.MyProjects.Items;
using AngorApp.UI.Sections.MyProjects.ManageFunds;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Kernel;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell.Utils;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.MyProjects;

[Section("My Projects", icon: "fa-regular fa-file-lines", sortIndex: 4)]
[SectionGroup("FOUNDER")]
public partial class MyProjectsSectionViewModel : ReactiveObject, IMyProjectsSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    private readonly IProjectAppService projectAppService;
    private readonly IProjectInvestCommandFactory projectInvestCommandFactory;
    private readonly IWalletContext walletContext;
    private readonly Func<ProjectId, IManageFundsViewModel> manageFundsFactory;
    private readonly INavigator navigator;
    private readonly BehaviorSubject<int> projectStatsLoadTotalCount = new(0);
    private readonly BehaviorSubject<int> projectStatsLoadCompletedCount = new(0);

    public MyProjectsSectionViewModel(
        UIServices uiServices,
        IProjectAppService projectAppService,
        IProjectInvestCommandFactory projectInvestCommandFactory,
        ICreateProjectFlow createProjectFlow,
        IWalletContext walletContext,
        Func<ProjectId, IManageFundsViewModel> manageFundsFactory,
        INavigator navigator)
    {
        this.projectAppService = projectAppService;
        this.projectInvestCommandFactory = projectInvestCommandFactory;
        this.walletContext = walletContext;
        this.manageFundsFactory = manageFundsFactory;
        this.navigator = navigator;

        var projectsCollection = RefreshableCollection.Create(DoLoadProjects, item => item.Project.Id).DisposeWith(disposable);

        LoadProjects = projectsCollection.Refresh;
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to load projects").DisposeWith(disposable);
        RefreshProjectStats = EnhancedCommand.CreateWithResult(DoRefreshProjectStats).DisposeWith(disposable);
        RefreshProjectStats.HandleErrorsWith(uiServices.NotificationService, "Failed to refresh project statistics").DisposeWith(disposable);

        LoadProjects.Successes()
            .ToSignal()
            .InvokeCommand(RefreshProjectStats)
            .DisposeWith(disposable);

        var projectChanges = projectsCollection.Changes;
        Projects = projectsCollection.Items;

        ActiveProjectsCount = projectChanges.FilterOnObservable(IsActive).Count();

        TotalRaised = projectChanges.TransformOnObservable(item =>
                                    {
                                        if (item.Project is IInvestmentProject inv)
                                            return inv.Raised;
                                        if (item.Project is IFundProject fnd)
                                            return fnd.Funded;
                                        return Observable.Return<IAmountUI>(new AmountUI(0));
                                    })
                                    .ForAggregation()
                                    .Sum(x => x.Sats)
                                    .Select(sats => new AmountUI(sats));

        Create = ReactiveCommand.CreateFromTask(createProjectFlow.CreateProject)
            .Enhance()
            .DisposeWith(disposable);
        Create.HandleErrorsWith(uiServices.NotificationService, "Cannot create project").DisposeWith(disposable);

        ProjectStatsLoadTotalCount = projectStatsLoadTotalCount;
        ProjectStatsLoadCompletedCount = projectStatsLoadCompletedCount;
    }

    private static IObservable<bool> IsActive(IMyProjectItem item)
    {
        if (item.Project is IFundProject)
        {
            return Observable.Return(true);
        } else if (item.Project is IInvestmentProject investmentProject)
        {
            return investmentProject.IsFundingOpen;
        }
        
        return Observable.Return(false);
    }

    private async Task<Result<IEnumerable<IMyProjectItem>>> DoLoadProjects()
    {
        // await Task.Delay(5000);
        return await walletContext
            .Require()
            .Bind(wallet => projectAppService.GetFounderProjects(wallet.Id))
            .Map(response => response.Projects.Select(dto => (IMyProjectItem)new MyProjectItem(dto, projectAppService, projectInvestCommandFactory, manageFundsFactory, navigator)));
    }

    private async Task<Result> DoRefreshProjectStats()
    {
        var items = Projects.ToList();
        projectStatsLoadTotalCount.OnNext(items.Count);
        projectStatsLoadCompletedCount.OnNext(0);

        var completed = 0;
        foreach (var item in items)
        {
            item.Project.Refresh.Execute(null);
            completed++;
            projectStatsLoadCompletedCount.OnNext(completed);
        }

        return Result.Success();
    }

    public IReadOnlyCollection<IMyProjectItem> Projects { get; }
    public IEnhancedCommand<Result<IEnumerable<IMyProjectItem>>> LoadProjects { get; }
    public IEnhancedCommand<Result> RefreshProjectStats { get; }
    public IEnhancedCommand<Result<Maybe<string>>> Create { get; }
    public IObservable<int> ActiveProjectsCount { get; }
    public IObservable<IAmountUI> TotalRaised { get; }
    public IObservable<int> ProjectStatsLoadTotalCount { get; }
    public IObservable<int> ProjectStatsLoadCompletedCount { get; }

    public void Dispose()
    {
        projectStatsLoadTotalCount.Dispose();
        projectStatsLoadCompletedCount.Dispose();
        disposable.Dispose();
    }
}
