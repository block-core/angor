using System.Collections.ObjectModel;
using System.Reactive.Linq;
using AngorApp.UI.Sections.MyProjects.Items;

namespace AngorApp.UI.Sections.MyProjects;

public class MyProjectsSectionViewModelSample : IMyProjectsSectionViewModel
{
    public MyProjectsSectionViewModelSample()
    {
        Projects = new ReadOnlyCollection<IMyProjectItem>(
        [
            new MyProjectItemSample
            {
                Name = "Zap AI",
                Description = "AI-powered bot on Nostr. Ask it anything and pay with Lightning.",
                InvestorsCount = Observable.Return(3),
                FundingRaised = Observable.Return(new AmountUI(23456000)) ,
                FundingTarget = new AmountUI(50000000),
                ProjectTypeLabel = "INVEST",
                FundingStatus = "Closed",
                IsFundingOpen = false,
                BannerUrl = new Uri("https://images-assets.nasa.gov/image/PIA05062/PIA05062~thumb.jpg"),
                LogoUrl = new Uri("https://www.nostria.app/assets/icons/icon-512x512-margin.png"),
                ProjectType = ProjectType.Invest,
                ProjectStatus = Observable.Return(ProjectStatus.Closed)
            },
            new MyProjectItemSample
            {
                Name = "Founder Hub",
                Description = "Launch and manage your fundraising campaigns with ease.",
                InvestorsCount = Observable.Return(14),
                FundingRaised = Observable.Return(new AmountUI(120000000)),
                FundingTarget = new AmountUI(200000000),
                ProjectTypeLabel = "INVEST",
                FundingStatus = "Open",
                IsFundingOpen = true,
                BannerUrl = new Uri("https://theunpluggednetwork.com/wp-content/uploads/2025/03/App-Testimonial-5-600x152.jpg"),
                LogoUrl = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg"),
                ProjectType = ProjectType.Invest,
                ProjectStatus = Observable.Return(ProjectStatus.Open)
            }
        ]);

        ActiveProjectsCount = Observable.Return(2);
        TotalRaised = Observable.Return(new AmountUI(232144));
        LoadProjects = ReactiveCommand.Create(() => Result.Success<IEnumerable<IMyProjectItem>>([])).Enhance();
        Create = ReactiveCommand.Create(() => Result.Success(Maybe<string>.None)).Enhance();
    }

    public IReadOnlyCollection<IMyProjectItem> Projects { get; }
    public IEnhancedCommand<Result<IEnumerable<IMyProjectItem>>> LoadProjects { get; }
    public IEnhancedCommand<Result<Maybe<string>>> Create { get; }
    public IObservable<int> ActiveProjectsCount { get; }
    public IObservable<IAmountUI> TotalRaised { get; }
}
