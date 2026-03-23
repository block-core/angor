using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Angor.Sdk.Funding.Projects;
using AngorApp.Core.Factories;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using DynamicData;
using DynamicData.Aggregation;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.MyProjects;

[Section("My Projects", icon: "fa-regular fa-file-lines", sortIndex: 4)]
[SectionGroup("FOUNDER")]
public partial class MyProjectsSectionViewModel : ReactiveObject, IMyProjectsSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    private readonly IProjectAppService projectAppService;
    private readonly IProjectFactory projectFactory;
    private readonly IWalletContext walletContext;
    private readonly BehaviorSubject<int> projectStatsLoadTotalCount = new(0);
    private readonly BehaviorSubject<int> projectStatsLoadCompletedCount = new(0);

    public MyProjectsSectionViewModel(
        UIServices uiServices,
        IProjectAppService projectAppService,
        IProjectFactory projectFactory,
        ICreateProjectFlow createProjectFlow,
        IWalletContext walletContext)
    {
        this.projectAppService = projectAppService;
        this.projectFactory = projectFactory;
        this.walletContext = walletContext;

        var projectsCollection = RefreshableCollection.Create(DoLoadProjects, item => item.Id).DisposeWith(disposable);

        LoadProjects = projectsCollection.Refresh;
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to load projects").DisposeWith(disposable);

        var projectChanges = projectsCollection.Changes;
        Projects = projectsCollection.Items;

        ActiveProjectsCount = projectChanges.FilterOnObservable(IsActive).Count();

        TotalRaised = projectChanges.TransformOnObservable(item =>
                                    {
                                        if (item is IInvestmentProject inv)
                                            return inv.Raised;
                                        if (item is IFundProject fnd)
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
    }

    private static IObservable<bool> IsActive(IProject item)
    {
        if (item is IFundProject)
        {
            return Observable.Return(true);
        } else if (item is IInvestmentProject investmentProject)
        {
            return investmentProject.IsFundingOpen;
        }
        
        return Observable.Return(false);
    }

    private async Task<Result<IEnumerable<IProject>>> DoLoadProjects()
    {
        return await walletContext
            .Require()
            .Bind(wallet => projectAppService.GetFounderProjects(wallet.Id))
            .Map(response => response.Projects.Select(projectFactory.Create));
    }

    public IReadOnlyCollection<IProject> Projects { get; }
    public IEnhancedCommand<Result<IEnumerable<IProject>>> LoadProjects { get; }
    public IEnhancedCommand<Result<Maybe<string>>> Create { get; }
    public IObservable<int> ActiveProjectsCount { get; }
    public IObservable<IAmountUI> TotalRaised { get; }

    public void Dispose()
    {
        projectStatsLoadTotalCount.Dispose();
        projectStatsLoadCompletedCount.Dispose();
        disposable.Dispose();
    }
}
