using Angor.Projects.Domain;

namespace Angor.Projects.Infrastructure.Impl;

public static class ProjectDataExtensions
{
    public static Project ToProject(this ProjectData data)
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

        var project = new Project()
        {
            Id = new ProjectId(data.IndexerData.ProjectIdentifier),

            Name = data.NostrMetadata.Name,
            ShortDescription = data.NostrMetadata.About,
            FounderKey = data.ProjectInfo.FounderKey,
            FounderRecoveryKey = data.ProjectInfo.FounderRecoveryKey,
            ExpiryDate = data.ProjectInfo.ExpiryDate,

            Picture = string.IsNullOrWhiteSpace(data.NostrMetadata.Picture)
                ? null
                : new Uri(data.NostrMetadata.Picture),

            Banner = string.IsNullOrWhiteSpace(data.NostrMetadata.Banner)
                ? null
                : new Uri(data.NostrMetadata.Banner),

            TargetAmount = data.ProjectInfo.TargetAmount,
            StartingDate = data.ProjectInfo.StartDate,
            PenaltyDuration = TimeSpan.FromDays(data.ProjectInfo.PenaltyDays),

            NostrNpubKey = data.ProjectInfo.NostrPubKey,

            InformationUri = string.IsNullOrWhiteSpace(data.NostrMetadata.Website)
                ? null
                : new Uri(data.NostrMetadata.Website)
        };

        project.Stages = data.ProjectInfo.Stages
            .Select((stage, index) => new Stage
            {
                ReleaseDate = stage.ReleaseDate,
                Amount = (long)(stage.AmountToRelease / 100 * data.ProjectInfo.TargetAmount * 1_0000_0000),
                Index = index + 1,
                Weight = (double)(stage.AmountToRelease / 100)
            })
            .ToList();

        return project;
    }
}