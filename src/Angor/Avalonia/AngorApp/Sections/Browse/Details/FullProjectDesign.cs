using Angor.Contexts.Funding.Projects.Domain;
using Angor.UI.Model.Implementation.Projects;

namespace AngorApp.Sections.Browse.Details;

public class FullProjectDesign : IFullProject
{
    public ProjectStatus Status  { get; } = ProjectStatus.Funding;
    public ProjectId ProjectId { get; } = new ProjectId("test-project-id");
    public IAmountUI TargetAmount { get; } = new AmountUI(200);

    public IEnumerable<IStage> Stages { get; } =
    [
        new StageDesign() { Amount = 123, Index = 0, RatioOfTotal = 0.25m, ReleaseDate = DateTime.Now.AddDays(3) },
        new StageDesign() { Amount = 123, Index = 1, RatioOfTotal = 0.25m, ReleaseDate = DateTime.Now.AddDays(6) },
        new StageDesign() { Amount = 123, Index = 2, RatioOfTotal = 0.5m, ReleaseDate = DateTime.Now.AddDays(12) }
    ];
    public string Name { get; } = "Cruzada21 - VEINTIUNO.LAT";
    public TimeSpan PenaltyDuration { get; } = TimeSpan.FromDays(90);
    public IAmountUI RaisedAmount { get; } = new AmountUI(100);
    public int? TotalInvestors { get; } = 42;
    public DateTime FundingStartDate { get; } = DateTime.Now.AddDays(-10);
    public DateTime FundingEndDate { get; } = DateTime.Now.AddDays(12);
    public TimeSpan TimeToFundingEndDate { get; } = TimeSpan.FromDays(12);
    public TimeSpan FundingPeriod { get; } = TimeSpan.FromDays(22);
    public TimeSpan TimeFromFundingStartingDate { get; } = TimeSpan.FromDays(10);
    public string NostrNpubKeyHex { get; } = "ca6e84aa974d00af805a754b34bc4e3c9a899aac14487a6f2e21fe9ea4b9fe43";
    public Uri? Avatar { get; } = new ("https://r2.primal.net/cache/c/41/88/c4188ab0115162d3172c16ad2c45ed1dd57b39794907cfd5308a08af5f4269a2.jpg");
    public string ShortDescription { get; } = "VEINTIUNO.LAT is a grassroots initiative uniting Bitcoin circular economies in Latam. Our mission is to strengthen regional adoption through practical, community-driven actions, empowering communities, onboard new merchants and lead individuals towards self-sovereignty. Using open sources tools and standards. Cruzada21 is official launch of VEINTIUNO.LAT.";
    public Uri? Banner { get; } = new Uri("https://r2.primal.net/cache/9/53/03/953038e1ce3bacfdfa9110e0905bd385c1a4ca884b914df2d34245fde957ff46.jpg");
}