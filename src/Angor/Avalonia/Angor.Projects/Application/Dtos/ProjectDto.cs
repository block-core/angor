using Angor.Projects.Domain;

namespace Angor.Projects.Application.Dtos;

public record ProjectDto
{
    public ProjectId Id { get; set; }
    public string Name { get; set; }
    public string ShortDescription { get; set; }
    public Uri? Picture { get; set; }
    public Uri? Icon { get; set; }
    public long TargetAmount { get; set; }
    public DateOnly StartingDate { get; set; }
    public TimeSpan PenaltyDuration { get; set; }
    public string NpubKey { get; set; }
    public string? NpubKeyHex { get; set; }
    public Uri? InformationUri { get; set; }
    public List<StageDto> Stages { get; set; }
}