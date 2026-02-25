using AngorApp.UI.Sections.MyProjects;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Shared;

public class ProjectItemSample : IProjectItem
{

    public ProjectItemSample()
    {
        Name = "Founder Hub";
        Description = "Launch and manage your fundraising campaigns with ease.";
        InvestorsCount = Observable.Return(14);
        FundingRaised = Observable.Return(new AmountUI(120000000));
        FundingTarget = new AmountUI(200000000);
        BannerUrl = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg");
        LogoUrl = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg");
        ProjectType = ProjectType.Invest;
        ProjectStatus = Observable.Return(AngorApp.UI.Sections.Shared.ProjectStatus.Open);
    }
    public ProjectId Id { get; set; } = new("test-project-id");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IAmountUI FundingTarget { get; set; } = new AmountUI(0);
    public IObservable<IAmountUI> FundingRaised { get; set; } = Observable.Return(new AmountUI(0));
    public IObservable<int> InvestorsCount { get; set; } = Observable.Return(0);
    public Uri? BannerUrl { get; set; }
    public Uri? LogoUrl { get; set; }
    public IEnhancedCommand<Result<IFullProject>> Refresh { get; } = ReactiveCommand.Create(() => Result.Failure<IFullProject>("Design-only command")).Enhance();
    public ProjectType ProjectType { get; set; }
    public IObservable<ProjectStatus> ProjectStatus { get; set; } = Observable.Return(Shared.ProjectStatus.Open);
    public IObservable<string> ErrorMessage { get; } = Observable.Never<string>();
}
