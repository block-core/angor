using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Zafiro.Avalonia.Controls;

namespace Angor.UI.Model.Implementation.Projects;

public class FullProject(ProjectDto info, ProjectStatisticsDto stats) : IFullProject
{
    public ProjectStatus Status => this.Status();

    public ProjectId ProjectId => info.Id;
    public IAmountUI TargetAmount => new AmountUI(info.TargetAmount);
    public IEnumerable<IStage> Stages => info.Stages.Select(IStage (dto) => new Stage()
    {
        Amount = dto.Amount,
        Index = dto.Index,
        ReleaseDate = dto.ReleaseDate,
        RatioOfTotal = dto.RatioOfTotal
    });

    public IAmountUI AvailableBalance => new AmountUI(stats.AvailableBalance);
    public int AvailableTransactions => stats.AvailableTransactions;
    public IAmountUI SpentAmount => new AmountUI(stats.SpentAmount);
    public int TotalTransactions => stats.TotalTransactions;
    public IAmountUI TotalInvested => new AmountUI(stats.TotalInvested);
    public IAmountUI WithdrawableAmount => new AmountUI(stats.WithdrawableAmount);
    public NextStageDto? NextStage { get; } = null!;
    public int SpentTransactions { get; set; }
    public string Name => info.Name;
    public TimeSpan PenaltyDuration => info.PenaltyDuration;
    public IAmountUI RaisedAmount => new AmountUI(stats.TotalInvested);
    public int? TotalInvestors => stats.TotalInvestors;
    public DateTime FundingStartDate => info.FundingStartDate;
    public DateTime FundingEndDate => info.FundingEndDate;
    public TimeSpan TimeToFundingEndDate => info.FundingEndDate - DateTime.Now;
    public TimeSpan FundingPeriod => info.FundingEndDate - info.FundingStartDate;
    public TimeSpan TimeFromFundingStartingDate => DateTime.Now - info.FundingStartDate;
    public string NostrNpubKeyHex => info.NostrNpubKeyHex;
    public Uri? Avatar => info.Avatar;
    public string ShortDescription => info.ShortDescription;
    public Uri? Banner => info.Banner;
}
