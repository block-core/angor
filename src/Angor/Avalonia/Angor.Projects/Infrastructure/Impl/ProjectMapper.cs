using Angor.Projects.Application.Dtos;
using Angor.Projects.Domain;

namespace Angor.Projects.Infrastructure.Impl;

public static class ProjectMapper
{
    public static ProjectDto ToProject(this ProjectData data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (data.ProjectInfo is null)
        {
            throw new ArgumentException("ProjectData.ProjectInfo cannot be null.", nameof(data));
        }

        if (data.NostrMetadata is null)
        {
            throw new ArgumentException("ProjectData.NostrMetadata cannot be null.", nameof(data));
        }

        var project = new ProjectDto()
        {
            Id = new ProjectId(data.IndexerData.ProjectIdentifier),

            Name = data.NostrMetadata.Name,
            ShortDescription = data.NostrMetadata.About,

            Picture = string.IsNullOrWhiteSpace(data.NostrMetadata.Picture)
                ? null
                : new Uri(data.NostrMetadata.Picture),

            Banner = string.IsNullOrWhiteSpace(data.NostrMetadata.Banner)
                ? null
                : new Uri(data.NostrMetadata.Banner),

            TargetAmount = data.ProjectInfo.TargetAmount,
            StartingDate = DateOnly.FromDateTime(data.ProjectInfo.StartDate),
            PenaltyDuration = TimeSpan.FromDays(data.ProjectInfo.PenaltyDays),

            NpubKey = data.ProjectInfo.NostrPubKey,
            NpubKeyHex = data.IndexerData?.NostrEventId,

            InformationUri = string.IsNullOrWhiteSpace(data.NostrMetadata.Website)
                ? null
                : new Uri(data.NostrMetadata.Website)
        };

        project.Stages = data.ProjectInfo.Stages
            .Select((stage, index) => new StageDto
            {
                ReleaseDate = DateOnly.FromDateTime(stage.ReleaseDate),
                Amount = (long)(stage.AmountToRelease/100 * data.ProjectInfo.TargetAmount * 1_0000_0000),
                Index = index + 1,
                Weight = (double)(stage.AmountToRelease / 100)
            })
            .ToList();

        return project;
    }
}