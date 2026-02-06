using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Shared.Models;

namespace Angor.Sdk.Funding.Projects.Dtos;

public class CreateProjectDto
{
    // Properties for the nostr profile
    public required string ProjectName { get; init; }
    public string? WebsiteUri { get; init; }
    public required string Description { get; init; }
    public required string AvatarUri { get; init; }
    public required string BannerUri { get; init; }
    public string? Nip05 { get; init; }
    public string? Lud16 { get; init; }
    public string? Nip57 { get; init; }
    
    // Properties for the project information
    public ProjectType ProjectType { get; init; } = ProjectType.Invest;
    public required long? Sats { get; init; }
    public required DateTime StartDate { get; init;}
    public DateTime? EndDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public required Amount TargetAmount { get; init; }
    public required int PenaltyDays { get; set; }
    public long? PenaltyThreshold { get; init; }
    public required IEnumerable<CreateProjectStageDto> Stages { get; init; }
    
    // For Fund and Subscribe types - support multiple patterns
    public List<DynamicStagePattern>? SelectedPatterns { get; init; }
    public int? PayoutDay { get; init; }
}

public record CreateProjectStageDto(DateOnly startDate, decimal PercentageOfTotal);
