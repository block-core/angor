using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Shared;
using AngorApp.Model.Projects;

namespace AngorApp.UI.Sections.Browse.Details;

public class FullProjectSample : IFullProject
{
    public ProjectStatus Status  { get; } = ProjectStatus.Funding;
    public ProjectId ProjectId { get; } = new ProjectId("test-project-id");
    public IAmountUI TargetAmount { get; } = new AmountUI(200);

    public IEnumerable<IStage> Stages { get; } =
    [
        new StageSample() { Amount = 123, Index = 0, RatioOfTotal = 0.25m, ReleaseDate = DateTime.Now.AddDays(3) },
        new StageSample() { Amount = 123, Index = 1, RatioOfTotal = 0.25m, ReleaseDate = DateTime.Now.AddDays(6) },
        new StageSample() { Amount = 123, Index = 2, RatioOfTotal = 0.5m, ReleaseDate = DateTime.Now.AddDays(12) }
    ];
    public string Name { get; } = "Cruzada21 - VEINTIUNO.LAT";
    public TimeSpan PenaltyDuration { get; } = TimeSpan.FromDays(90);
    public IAmountUI? PenaltyThreshold { get; } = new AmountUI(50000000); // 0.5 BTC in satoshis
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
    public IAmountUI AvailableBalance { get; }
    public int AvailableTransactions { get; }
    public IAmountUI SpentAmount { get; }
    public int TotalTransactions { get; }
    public IAmountUI TotalInvested { get; }
    public IAmountUI WithdrawableAmount { get; }
    public NextStageDto? NextStage { get; }
    public int SpentTransactions { get; set; }
    public string FounderPubKey { get; } = "some npub key";
}