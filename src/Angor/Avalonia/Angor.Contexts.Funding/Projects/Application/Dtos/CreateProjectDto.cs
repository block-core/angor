using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Projects.Application.Dtos;

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
    public required long? Sats { get; init; }
    public required DateTime StartDate { get; init;}
    public DateTime? EndDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public bool? EnforceTargetAmount { get; set; }
    public long? PenaltyThreshold { get; set; }
    public required Amount TargetAmount { get; init; }
    public required int PenaltyDays { get; set; }
    public required IEnumerable<CreateProjectStageDto> Stages { get; init; }
}

public record CreateProjectStageDto(DateOnly startDate, decimal PercentageOfTotal);
