using System.Reactive.Disposables;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using AngorApp.UI.Sections.MyProjects;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Shared;

public partial class ProjectItem : ReactiveObject, IProjectItem, IDisposable
{
    private readonly ProjectDto dto;
    private readonly IProjectAppService projectAppService;
    private readonly CompositeDisposable disposable = new();

    public ProjectItem(ProjectDto dto, IProjectAppService projectAppService)
    {
        this.dto = dto;
        this.projectAppService = projectAppService;

        LoadFullProject = EnhancedCommand.CreateWithResult(DoLoadFullProject).DisposeWith(disposable);
        ErrorMessage = LoadFullProject.Failures().ReplayLastActive();

        ProjectType = GetProjectType(dto);
        ProjectStatus = LoadFullProject.Successes().Select(statisticsDto => statisticsDto.IsFailed() ? Shared.ProjectStatus.Closed : Shared.ProjectStatus.Open);
        InvestorsCount = LoadFullProject.Successes().Select(statisticsDto => statisticsDto.TotalInvestors ?? 0);
        FundingRaised = LoadFullProject.Successes().Select(statisticsDto => statisticsDto.RaisedAmount);
    }

    public IObservable<string> ErrorMessage { get; }

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

    private Task<Result<IFullProject>> DoLoadFullProject()
    {
        return projectAppService.GetProjectStatistics(dto.Id).Map(IFullProject (statisticsDto) => new FullProject(dto, statisticsDto));
    }

    public ProjectId Id => dto.Id;
    public string Name => dto.Name;
    public string Description => dto.ShortDescription;
    public IAmountUI FundingTarget => new AmountUI(dto.TargetAmount);
    public IObservable<IAmountUI> FundingRaised { get; }
    public IObservable<int> InvestorsCount { get; }
    public Uri? BannerUrl => dto.Banner;
    public Uri? LogoUrl => dto.Avatar;
    public IEnhancedCommand<Result<IFullProject>> LoadFullProject { get; }
    public ProjectType ProjectType { get; }
    public IObservable<ProjectStatus> ProjectStatus { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
