using ProjectItemStatus = AngorApp.UI.Sections.Shared.Project.ProjectStatus;

namespace AngorApp.UI.Sections.Shared.Project;

public sealed class FundProjectSample : ProjectSample, IFundProject
{
    public FundProjectSample()
    {
        Name = "Atlas Growth Fund";
        Description = "Diversified fund focused on early-stage Bitcoin-native startups.";
        FundingTarget = new AmountUI(500000000);
        BannerUrl = new Uri("https://images-assets.nasa.gov/image/GSFC_20171208_Archive_e001861/GSFC_20171208_Archive_e001861~thumb.jpg");
        LogoUrl = new Uri("https://images-assets.nasa.gov/image/GSFC_20171208_Archive_e001861/GSFC_20171208_Archive_e001861~thumb.jpg");
        ApplyStats(CreateFundStats(
            fundingRaised: new AmountUI(312000000),
            investorsCount: 42,
            status: ProjectItemStatus.Open));
    }
}
