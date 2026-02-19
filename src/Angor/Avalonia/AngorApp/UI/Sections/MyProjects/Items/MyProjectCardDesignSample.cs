using System.Reactive.Linq;

namespace AngorApp.UI.Sections.MyProjects.Items;

public class MyProjectCardDesignSample : MyProjectItemSample
{
    public MyProjectCardDesignSample()
    {
        Name = "Founder Hub";
        Description = "Launch and manage your fundraising campaigns with ease.";
        InvestorsCount = Observable.Return(14);
        FundingRaised = Observable.Return(new AmountUI(120000000));
        FundingTarget = new AmountUI(200000000);
        BannerUrl = new Uri("https://theunpluggednetwork.com/wp-content/uploads/2025/03/App-Testimonial-5-600x152.jpg");
        LogoUrl = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg");
        ProjectType = ProjectType.Invest;
        ProjectStatus = Observable.Return(global::AngorApp.UI.Sections.MyProjects.ProjectStatus.Open);
    }
}
