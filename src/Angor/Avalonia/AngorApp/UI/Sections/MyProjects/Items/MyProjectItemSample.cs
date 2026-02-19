using Angor.Sdk.Funding.Projects.Dtos;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.MyProjects.Items;

public class MyProjectItemSample : IMyProjectItem
{
    public ProjectId Id { get; set; } = new("test-project-id");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IAmountUI FundingTarget { get; set; } = new AmountUI(0);
    public IObservable<IAmountUI> FundingRaised { get; set; }  = Observable.Return(new AmountUI(0));
    public IObservable<int> InvestorsCount { get; set; } = Observable.Return(0);
    public Uri? BannerUrl { get; set; }
    public Uri? LogoUrl { get; set; }
    public string ProjectTypeLabel { get; set; } = "INVEST";
    public string FundingStatus { get; set; } = "Open";
    public bool IsFundingOpen { get; set; } = true;
    public IEnhancedCommand ManageFunds { get; } = EnhancedCommand.Create(() => { });
    public IEnhancedCommand<Result<IFullProject>> LoadFullProject { get; } = ReactiveCommand.Create(() => Result.Failure<IFullProject>("Design-only command")).Enhance();
    public ProjectType ProjectType { get; set; }
    public IObservable<ProjectStatus> ProjectStatus { get; set; } = Observable.Return(global::AngorApp.UI.Sections.MyProjects.ProjectStatus.Open);
    public IObservable<string> ErrorMessage { get; } = Observable.Never<string>();
}
