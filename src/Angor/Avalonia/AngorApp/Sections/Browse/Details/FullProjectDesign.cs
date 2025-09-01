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
    public string Name { get; } = "Test Project";
    public TimeSpan PenaltyDuration { get; } = TimeSpan.FromDays(90);
    public IAmountUI RaisedAmount { get; } = new AmountUI(100);
    public int? TotalInvestors { get; } = 42;
    public DateTime FundingStartDate { get; } = DateTime.Now.AddDays(-10);
    public DateTime FundingEndDate { get; } = DateTime.Now.AddDays(12);
    public TimeSpan TimeToFundingEndDate { get; } = TimeSpan.FromDays(12);
    public TimeSpan FundingPeriod { get; } = TimeSpan.FromDays(22);
    public TimeSpan TimeFromFundingStartingDate { get; } = TimeSpan.FromDays(10);
    public string NostrNpubKeyHex { get; } = "ca6e84aa974d00af805a754b34bc4e3c9a899aac14487a6f2e21fe9ea4b9fe43";
    public Uri? Avatar { get; } = new ("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600");
    public string ShortDescription { get; } = "This is a short description of the test project.";
    public Uri? Banner { get; } = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600");
}