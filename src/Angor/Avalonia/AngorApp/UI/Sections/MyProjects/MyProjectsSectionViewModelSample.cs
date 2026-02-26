using System.Collections.ObjectModel;
using System.Reactive.Linq;
using AngorApp.UI.Sections.MyProjects.Items;
using AngorApp.UI.Sections.Shared;
using AngorApp.UI.Sections.Shared.Project;

namespace AngorApp.UI.Sections.MyProjects;

public class MyProjectsSectionViewModelSample : IMyProjectsSectionViewModel
{
    public MyProjectsSectionViewModelSample()
    {
        Projects = new ReadOnlyCollection<IMyProjectItem>(
        [
            new MyProjectItemSample
            {
                Project = new InvestmentProjectSample
                {
                    Name = "Zap AI",
                    Description = "AI-powered bot on Nostr. Ask it anything and pay with Lightning.",
                    FundingTarget = new AmountUI(50000000),
                    BannerUrl = new Uri("https://images-assets.nasa.gov/image/PIA05062/PIA05062~thumb.jpg"),
                    LogoUrl = new Uri("https://www.nostria.app/assets/icons/icon-512x512-margin.png"),
                }
            },
            new MyProjectItemSample
            {
                Project = new FundProjectSample
                {
                    Name = "Community Treasury Fund",
                    Description = "Pooled funding vehicle for ecosystem grants and operations.",
                    FundingTarget = new AmountUI(350000000),
                    BannerUrl = new Uri("https://theunpluggednetwork.com/wp-content/uploads/2025/03/App-Testimonial-5-600x152.jpg"),
                    LogoUrl = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg"),
                }
            },
            new MyProjectItemSample
            {
                Project = new SubscriptionProjectSample
                {
                    Name = "Relay Monitor Pro",
                    Description = "Subscription-based uptime monitoring and incident alerts for relay operators.",
                    FundingTarget = new AmountUI(100000000),
                }
            }
        ]);

        ActiveProjectsCount = Observable.Return(3);
        TotalRaised = Observable.Return(new AmountUI(232144));
        LoadProjects = ReactiveCommand.Create(() => Result.Success<IEnumerable<IMyProjectItem>>([])).Enhance();
        RefreshProjectStats = ReactiveCommand.Create(() => Result.Success()).Enhance();
        Create = ReactiveCommand.Create(() => Result.Success(Maybe<string>.None)).Enhance();
        ProjectStatsLoadTotalCount = Observable.Return(3);
        ProjectStatsLoadCompletedCount = Observable.Return(3);
    }

    public IReadOnlyCollection<IMyProjectItem> Projects { get; }
    public IEnhancedCommand<Result<IEnumerable<IMyProjectItem>>> LoadProjects { get; }
    public IEnhancedCommand<Result> RefreshProjectStats { get; }
    public IEnhancedCommand<Result<Maybe<string>>> Create { get; }
    public IObservable<int> ActiveProjectsCount { get; }
    public IObservable<IAmountUI> TotalRaised { get; }
    public IObservable<int> ProjectStatsLoadTotalCount { get; }
    public IObservable<int> ProjectStatsLoadCompletedCount { get; }
}
