using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;

namespace Angor.Sdk.Funding.Projects.Dtos;

public record ProjectDto
{
    public ProjectId Id { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public Uri? Avatar { get; set; }
    public Uri? Banner { get; set; }
    public long TargetAmount { get; set; }
    public DateTime FundingStartDate { get; set; }
    public DateTime FundingEndDate { get; set; }
    public TimeSpan PenaltyDuration { get; set; }
    public long? PenaltyThreshold { get; set; }
    public string NostrNpubKeyHex { get; set; } = string.Empty;
    public Uri? InformationUri { get; set; }
    public List<StageDto> Stages { get; set; } = new();
    public string FounderPubKey { get; set; } = string.Empty;
    
    // New fields for Fund/Subscribe support
    public int Version { get; set; } = 2;
    public ProjectType ProjectType { get; set; } = ProjectType.Invest;
    public List<DynamicStagePattern> DynamicStagePatterns { get; set; } = new();
}