using Angor.Sdk.Funding.Projects.Dtos;
using DynamicStagePattern = Angor.Shared.Models.DynamicStagePattern;

namespace AngorApp.UI.Sections.Shared.Project;

public interface IProjectStats
{
    ProjectType ProjectType { get; }
    ProjectStatus Status { get; }
    IAmountUI FundingRaised { get; }
    int InvestorsCount { get; }
}

public abstract record ProjectStats(
    ProjectType ProjectType,
    ProjectStatus Status,
    IAmountUI FundingRaised,
    int InvestorsCount) : IProjectStats;

public sealed record InvestmentProjectStats(
    ProjectType ProjectType,
    ProjectStatus Status,
    IAmountUI FundingRaised,
    int InvestorsCount,
    IReadOnlyList<StageDto> Stages,
    NextStageDto? NextStage) : ProjectStats(ProjectType, Status, FundingRaised, InvestorsCount);

public sealed record FundProjectStats(
    ProjectType ProjectType,
    ProjectStatus Status,
    IAmountUI FundingRaised,
    int InvestorsCount,
    IReadOnlyList<DynamicStagePattern> DynamicStagePatterns,
    IReadOnlyList<DynamicStageDto> DynamicStages) : ProjectStats(ProjectType, Status, FundingRaised, InvestorsCount);

public sealed record SubscriptionProjectStats(
    ProjectType ProjectType,
    ProjectStatus Status,
    IAmountUI FundingRaised,
    int InvestorsCount,
    IReadOnlyList<DynamicStagePattern> DynamicStagePatterns,
    IReadOnlyList<DynamicStageDto> DynamicStages) : ProjectStats(ProjectType, Status, FundingRaised, InvestorsCount);
