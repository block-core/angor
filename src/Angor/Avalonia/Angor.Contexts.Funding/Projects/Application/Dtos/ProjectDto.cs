using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Projects.Application.Dtos;

public record ProjectDto
{
    public ProjectId Id { get; set; }
    public string Name { get; set; }
    public string ShortDescription { get; set; }
    public Uri? Avatar { get; set; }
    public Uri? Banner { get; set; }
    public long TargetAmount { get; set; }
    public DateTime FundingStartDate { get; set; }
    public DateTime FundingEndDate { get; set; }
    public TimeSpan PenaltyDuration { get; set; }
    public string NostrNpubKeyHex { get; set; }
    public Uri? InformationUri { get; set; }
    public List<StageDto> Stages { get; set; }
}