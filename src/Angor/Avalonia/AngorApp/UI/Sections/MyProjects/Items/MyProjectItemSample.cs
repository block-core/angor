using AngorApp.UI.Sections.Shared;
using ProjectStatus = AngorApp.UI.Sections.Shared.ProjectStatus;

namespace AngorApp.UI.Sections.MyProjects.Items;

public class MyProjectItemSample : IMyProjectItem
{
    public IProjectItem Project { get; set; } = new ProjectItemSample()
    {
        Name = "Founder Hub",
        Description = "Launch and manage your fundraising campaigns with ease.",
        InvestorsCount = Observable.Return(14),
        FundingRaised = Observable.Return(new AmountUI(120000000)),
        FundingTarget = new AmountUI(200000000),
        BannerUrl = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg"),
        LogoUrl = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg"),
        ProjectType = ProjectType.Invest,
        ProjectStatus = Observable.Return(ProjectStatus.Open),
    };
    public IEnhancedCommand ManageFunds { get; } = EnhancedCommand.Create(() => { });
}
