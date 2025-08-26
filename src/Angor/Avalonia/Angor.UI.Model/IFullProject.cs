using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.UI.Model;

public interface IFullProject
{
    ProjectStatus Status { get; }
    ProjectId ProjectId { get; }
    IAmountUI TargetAmount { get; }
    IEnumerable<IStage> Stages { get; }
    string Name { get; }
    TimeSpan PenaltyDuration { get; }
    IAmountUI RaisedAmount { get; }
    int? TotalInvestors { get; }
    DateTime FundingStartDate { get; }
    DateTime FundingEndDate { get; }
    TimeSpan TimeToFundingEndDate { get; }
    TimeSpan FundingPeriod { get; }
    TimeSpan TimeFromFundingStartingDate { get; }
    public string NostrNpubKey { get; }
    public Uri? Avatar { get; }
    public string ShortDescription { get; }
    public Uri? Banner { get; }
}