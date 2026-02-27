using Angor.Sdk.Funding.Projects.Dtos;
using DynamicStagePattern = Angor.Shared.Models.DynamicStagePattern;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;
using ProjectItemStatus = AngorApp.UI.Sections.Shared.Project.ProjectStatus;

namespace AngorApp.UI.Sections.Shared.Project;

public class ProjectSample : IProject
{
    public ProjectSample()
    {
        Name = "Founder Hub";
        Description = "Launch and manage your fundraising campaigns with ease.";
        FundingTarget = new AmountUI(200000000);
        BannerUrl = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg");
        LogoUrl = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg");
        ApplyStats(CreateInvestmentStats(
            fundingRaised: new AmountUI(120000000),
            investorsCount: 14,
            status: ProjectItemStatus.Open));
    }

    protected void ApplyStats(IProjectStats stats)
    {
        Stats = Observable.Return(stats);
        FundingRaised = Observable.Return(stats.FundingRaised);
        InvestorsCount = Observable.Return(stats.InvestorsCount);
        ProjectType = stats.ProjectType;
        ProjectStatus = Observable.Return(stats.Status);
    }

    protected static InvestmentProjectStats CreateInvestmentStats(IAmountUI fundingRaised, int investorsCount, ProjectItemStatus status)
    {
        return new InvestmentProjectStats(
            ProjectType.Invest,
            status,
            fundingRaised,
            investorsCount,
            Array.Empty<StageDto>(),
            null);
    }

    protected static FundProjectStats CreateFundStats(IAmountUI fundingRaised, int investorsCount, ProjectItemStatus status)
    {
        return new FundProjectStats(
            ProjectType.Fund,
            status,
            fundingRaised,
            investorsCount,
            Array.Empty<DynamicStagePattern>(),
            Array.Empty<DynamicStageDto>());
    }

    protected static SubscriptionProjectStats CreateSubscriptionStats(IAmountUI fundingRaised, int investorsCount, ProjectItemStatus status)
    {
        return new SubscriptionProjectStats(
            ProjectType.Subscribe,
            status,
            fundingRaised,
            investorsCount,
            Array.Empty<DynamicStagePattern>(),
            Array.Empty<DynamicStageDto>());
    }

    public ProjectId Id { get; set; } = new("test-project-id");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IAmountUI FundingTarget { get; set; } = new AmountUI(0);
    public IObservable<IAmountUI> FundingRaised { get; set; } = Observable.Return(new AmountUI(0));
    public IObservable<int> InvestorsCount { get; set; } = Observable.Return(0);
    public Uri? BannerUrl { get; set; }
    public Uri? LogoUrl { get; set; }
    public IObservable<IProjectStats> Stats { get; set; } = Observable.Never<IProjectStats>();
    public IEnhancedCommand<Result<IProjectStats>> RefreshStats { get; } = ReactiveCommand.Create(() => Result.Failure<IProjectStats>("Design-only command")).Enhance();
    public IEnhancedCommand<Result<IFullProject>> Refresh { get; } = ReactiveCommand.Create(() => Result.Failure<IFullProject>("Design-only command")).Enhance();
    public ProjectType ProjectType { get; set; }
    public IObservable<ProjectStatus> ProjectStatus { get; set; } = Observable.Return(ProjectItemStatus.Open);
    public IObservable<string> ErrorMessage { get; } = Observable.Never<string>();
}
