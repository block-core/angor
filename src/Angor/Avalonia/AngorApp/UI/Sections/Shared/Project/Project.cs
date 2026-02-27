using System.Reactive.Disposables;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Shared.Project;

public partial class Project : ReactiveObject, IProject, IDisposable
{
    private readonly ProjectDto dto;
    private readonly IProjectAppService projectAppService;
    private readonly CompositeDisposable disposable = new();

    public Project(ProjectDto dto, IProjectAppService projectAppService)
    {
        this.dto = dto;
        this.projectAppService = projectAppService;

        RefreshStats = EnhancedCommand.CreateWithResult(DoRefreshStats).DisposeWith(disposable);
        Refresh = EnhancedCommand.CreateWithResult(DoLoadFullProject).DisposeWith(disposable);
        Stats = Observable.Merge(
                RefreshStats.Successes(),
                Refresh.Successes().Select(CreateStats))
            .ReplayLastActive();
        ErrorMessage = Observable.Merge(Refresh.Failures(), RefreshStats.Failures()).ReplayLastActive();

        ProjectType = GetProjectType(dto);
        ProjectStatus = Stats.Select(stats => stats.Status);
        InvestorsCount = Stats.Select(stats => stats.InvestorsCount);
        FundingRaised = Stats.Select(stats => stats.FundingRaised);
    }

    public static IProject Create(ProjectDto dto, IProjectAppService projectAppService)
    {
        return dto.ProjectType switch
        {
            Angor.Shared.Models.ProjectType.Invest => new InvestmentProject(dto, projectAppService),
            Angor.Shared.Models.ProjectType.Fund => new FundProject(dto, projectAppService),
            Angor.Shared.Models.ProjectType.Subscribe => new SubscriptionProject(dto, projectAppService),
            _ => throw new ArgumentOutOfRangeException(nameof(dto.ProjectType), dto.ProjectType, null)
        };
    }

    public IObservable<string> ErrorMessage { get; }
    public IObservable<IProjectStats> Stats { get; }

    private ProjectType GetProjectType(ProjectDto projectDto)
    {
        return projectDto.ProjectType switch
        {
            Angor.Shared.Models.ProjectType.Invest => ProjectType.Invest,
            Angor.Shared.Models.ProjectType.Fund => ProjectType.Fund,
            Angor.Shared.Models.ProjectType.Subscribe => ProjectType.Subscribe,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    protected ProjectDto Dto => dto;

    protected virtual IProjectStats CreateStats(ProjectStatisticsDto statisticsDto)
    {
        var (fundingRaised, investorsCount, status) = CreateCommonStats(statisticsDto);

        return ProjectType switch
        {
            ProjectType.Invest => new InvestmentProjectStats(ProjectType, status, fundingRaised, investorsCount, dto.Stages ?? [], statisticsDto.NextStage),
            ProjectType.Fund => new FundProjectStats(ProjectType, status, fundingRaised, investorsCount, dto.DynamicStagePatterns ?? [], statisticsDto.DynamicStages ?? []),
            ProjectType.Subscribe => new SubscriptionProjectStats(ProjectType, status, fundingRaised, investorsCount, dto.DynamicStagePatterns ?? [], statisticsDto.DynamicStages ?? []),
            _ => throw new ArgumentOutOfRangeException(nameof(ProjectType), ProjectType, null)
        };
    }

    protected virtual IProjectStats CreateStats(IFullProject fullProject)
    {
        var status = fullProject.Status == AngorApp.Model.Contracts.Projects.ProjectStatus.Failed
            ? Shared.Project.ProjectStatus.Closed
            : Shared.Project.ProjectStatus.Open;

        return ProjectType switch
        {
            ProjectType.Invest => new InvestmentProjectStats(ProjectType, status, fullProject.RaisedAmount, fullProject.TotalInvestors ?? 0, dto.Stages ?? [], fullProject.NextStage),
            ProjectType.Fund => new FundProjectStats(ProjectType, status, fullProject.RaisedAmount, fullProject.TotalInvestors ?? 0, dto.DynamicStagePatterns ?? [], fullProject.DynamicStages ?? []),
            ProjectType.Subscribe => new SubscriptionProjectStats(ProjectType, status, fullProject.RaisedAmount, fullProject.TotalInvestors ?? 0, dto.DynamicStagePatterns ?? [], fullProject.DynamicStages ?? []),
            _ => throw new ArgumentOutOfRangeException(nameof(ProjectType), ProjectType, null)
        };
    }

    protected virtual IFullProject CreateFullProject(ProjectStatisticsDto statisticsDto)
    {
        return new FullProject(dto, statisticsDto);
    }

    protected (IAmountUI FundingRaised, int InvestorsCount, ProjectStatus Status) CreateCommonStats(ProjectStatisticsDto statisticsDto)
    {
        var fundingRaised = new AmountUI(statisticsDto.TotalInvested);
        var fundingFinished = DateTime.UtcNow.Date >= dto.FundingEndDate.Date;
        var reachedTarget = statisticsDto.TotalInvested >= dto.TargetAmount;
        var status = fundingFinished && !reachedTarget ? Shared.Project.ProjectStatus.Closed : Shared.Project.ProjectStatus.Open;

        return (fundingRaised, statisticsDto.TotalInvestors ?? 0, status);
    }

    private Task<Result<ProjectStatisticsDto>> DoLoadStats()
    {
        return projectAppService.GetProjectStatistics(dto.Id);
    }

    private Task<Result<IProjectStats>> DoRefreshStats()
    {
        return DoLoadStats().Map(CreateStats);
    }

    private Task<Result<IFullProject>> DoLoadFullProject()
    {
        return DoLoadStats().Map(CreateFullProject);
    }

    public ProjectId Id => dto.Id;
    public string Name => dto.Name;
    public string Description => dto.ShortDescription;
    public IAmountUI FundingTarget => new AmountUI(dto.TargetAmount);
    public IObservable<IAmountUI> FundingRaised { get; }
    public IObservable<int> InvestorsCount { get; }
    public Uri? BannerUrl => dto.Banner;
    public Uri? LogoUrl => dto.Avatar;
    public IEnhancedCommand<Result<IProjectStats>> RefreshStats { get; }
    public IEnhancedCommand<Result<IFullProject>> Refresh { get; }
    public ProjectType ProjectType { get; }
    public IObservable<ProjectStatus> ProjectStatus { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
