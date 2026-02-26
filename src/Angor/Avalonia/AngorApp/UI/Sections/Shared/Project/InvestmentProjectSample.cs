using ProjectItemStatus = AngorApp.UI.Sections.Shared.Project.ProjectStatus;

namespace AngorApp.UI.Sections.Shared.Project;

public sealed class InvestmentProjectSample : ProjectSample, IInvestmentProject
{
    public InvestmentProjectSample()
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
}
