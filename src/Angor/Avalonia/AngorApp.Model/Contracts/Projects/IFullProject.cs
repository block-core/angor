using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Models;

namespace AngorApp.Model.Contracts.Projects;

public interface IFullProject
{
    ProjectStatus Status { get; }
    ProjectId ProjectId { get; }
    IAmountUI TargetAmount { get; }
    IEnumerable<IStage> Stages { get; }
    string Name { get; }
    TimeSpan PenaltyDuration { get; }
    IAmountUI? PenaltyThreshold { get; }
    IAmountUI RaisedAmount { get; }
    int? TotalInvestors { get; }
    DateTime FundingStartDate { get; }
    DateTime FundingEndDate { get; }
    TimeSpan TimeToFundingEndDate { get; }
    TimeSpan FundingPeriod { get; }
    TimeSpan TimeFromFundingStartingDate { get; }
    public string NostrNpubKeyHex { get; }
    public Uri? Avatar { get; }
    public string ShortDescription { get; }
    public Uri? Banner { get; }
    IAmountUI AvailableBalance { get; }
    int AvailableTransactions { get; }
    IAmountUI SpentAmount { get; }
    int TotalTransactions { get; }
    IAmountUI TotalInvested { get; }
    IAmountUI WithdrawableAmount { get; }
    NextStageDto? NextStage { get; }
    int SpentTransactions { get; set; }
    public string FounderPubKey { get; }
    
    // New properties for Fund/Subscribe support
    int Version { get; }
    ProjectType ProjectType { get; }
    List<DynamicStagePattern> DynamicStagePatterns { get; }
}