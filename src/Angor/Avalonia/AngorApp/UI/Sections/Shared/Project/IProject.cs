using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Shared.Project;

public interface IProject
{
    ProjectId Id { get; }
    string Name { get; }
    string Description { get; }
    IAmountUI FundingTarget { get; }
    IObservable<IAmountUI> FundingRaised { get; }
    IObservable<int> InvestorsCount { get; }
    Uri? BannerUrl { get; }
    Uri? LogoUrl { get; }
    IObservable<IProjectStats> Stats { get; }
    IEnhancedCommand<Result<IProjectStats>> RefreshStats { get; }
    IEnhancedCommand<Result<IFullProject>> Refresh { get; }
    ProjectType ProjectType { get; }
    IObservable<ProjectStatus> ProjectStatus { get; }
    IObservable<string> ErrorMessage { get; }
    public IObservable<double> FundingRatio => FundingRaised.Select(raised => FundingTarget.Sats != 0 ?  (double)raised.Sats / FundingTarget.Sats : 0);
}
