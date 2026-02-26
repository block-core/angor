using ProjectItemStatus = AngorApp.UI.Sections.Shared.Project.ProjectStatus;

namespace AngorApp.UI.Sections.Shared.Project;

public sealed class SubscriptionProjectSample : ProjectSample, ISubscriptionProject
{
    public SubscriptionProjectSample()
    {
        Name = "Nostr Insights Pro";
        Description = "Recurring subscription for analytics, alerts, and API access.";
        FundingTarget = new AmountUI(150000000);
        BannerUrl = new Uri("https://images-assets.nasa.gov/image/PIA12235/PIA12235~thumb.jpg");
        LogoUrl = new Uri("https://images-assets.nasa.gov/image/PIA12235/PIA12235~thumb.jpg");
        ApplyStats(CreateSubscriptionStats(
            fundingRaised: new AmountUI(87000000),
            investorsCount: 128,
            status: ProjectItemStatus.Open));
    }
}
