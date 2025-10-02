using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;

namespace Angor.UI.Model.Implementation.Projects;

public class FullProject(ProjectDto info, ProjectStatisticsDto stats) : IFullProject
{
    public ProjectDto Info { get; } = info;
    public ProjectStatisticsDto Stats { get; } = stats;
    public ProjectStatus Status => this.Status();

    public ProjectId ProjectId => Info.Id;
    public IAmountUI TargetAmount => new AmountUI(info.TargetAmount);
    public IEnumerable<IStage> Stages => info.Stages.Select(IStage (dto) => new Stage()
    {
        Amount = dto.Amount,
        Index = dto.Index,
        ReleaseDate = dto.ReleaseDate,
        RatioOfTotal = dto.RatioOfTotal
    });

    public string Name => Info.Name;
    public TimeSpan PenaltyDuration => Info.PenaltyDuration;
    public IAmountUI RaisedAmount => new AmountUI(Stats.TotalInvested);
    public int? TotalInvestors => Stats.TotalInvestors;
    public DateTime FundingStartDate => Info.FundingStartDate;
    public DateTime FundingEndDate => Info.FundingEndDate;
    public TimeSpan TimeToFundingEndDate => Info.FundingEndDate - DateTime.Now;
    public TimeSpan FundingPeriod => Info.FundingEndDate - Info.FundingStartDate;
    public TimeSpan TimeFromFundingStartingDate => DateTime.Now - Info.FundingStartDate;
    public string NostrNpubKeyHex => Info.NostrNpubKeyHex;
    public Uri? Avatar => Info.Avatar;
    public string ShortDescription => Info.ShortDescription;
    public Uri? Banner => info.Banner;
}
