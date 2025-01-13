using AngorApp.Model;

namespace Angor.Model.Implementation.Projects;

public static class ProjectMapper
{
    public static IProject ToProject(this ProjectData data)
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

        var project = new Project
        {
            Id = data.IndexerData?.ProjectIdentifier,

            Name = data.NostrMetadata.Name,
            ShortDescription = data.NostrMetadata.About,

            Picture = string.IsNullOrWhiteSpace(data.NostrMetadata.Picture)
                ? null
                : new Uri(data.NostrMetadata.Picture),

            Icon = string.IsNullOrWhiteSpace(data.NostrMetadata.Banner)
                ? null
                : new Uri(data.NostrMetadata.Banner),

            BitcoinAddress = data.ProjectInfo.FounderRecoveryKey,

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
            .Select((stage, index) => new Stage
            {
                ReleaseDate = DateOnly.FromDateTime(stage.ReleaseDate),
                Amount = (uint)stage.AmountToRelease,
                Index = index + 1,
                Weight = project.TargetAmount > 0
                    ? (double)(stage.AmountToRelease / project.TargetAmount)
                    : 0.0
            })
            .Cast<IStage>()
            .ToList();

        return project;
    }
}