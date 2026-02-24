using AngorApp.UI.Sections.MyProjects;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Shared;

public interface IProjectItem
{
    ProjectId Id { get; }
    string Name { get; }
    string Description { get; }
    IAmountUI FundingTarget { get; }
    IObservable<IAmountUI> FundingRaised { get; }
    IObservable<int> InvestorsCount { get; }
    Uri? BannerUrl { get; }
    Uri? LogoUrl { get; }
    IEnhancedCommand<Result<IFullProject>> LoadFullProject { get; }
    ProjectType ProjectType { get; }
    IObservable<ProjectStatus> ProjectStatus { get; }
    IObservable<string> ErrorMessage { get; }
}
