using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

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
            NostrPubKey = data.ProjectInfo.NostrPubKey,

            Picture = string.IsNullOrWhiteSpace(data.NostrMetadata.Picture)
                ? null
                : Uri.TryCreate(data.NostrMetadata.Picture, UriKind.RelativeOrAbsolute, out var pictureUri)
                    ? pictureUri
                    : null,

            Banner = string.IsNullOrWhiteSpace(data.NostrMetadata.Banner)
                ? null
                : Uri.TryCreate(data.NostrMetadata.Banner, UriKind.RelativeOrAbsolute, out var bannerUri)
                    ? bannerUri
                    : null,

            TargetAmount = data.ProjectInfo.TargetAmount,
            StartingDate = data.ProjectInfo.StartDate,
            PenaltyDuration = TimeSpan.FromDays(data.ProjectInfo.PenaltyDays),
            
            InformationUri = string.IsNullOrWhiteSpace(data.NostrMetadata.Website)
                ? null
                : Uri.TryCreate(data.NostrMetadata.Website, UriKind.RelativeOrAbsolute, out var uri)
                    ? uri
                    : null,
        };

        project.Stages = data.ProjectInfo.Stages
            .Select((stage, index) =>
            {
                var stageAmountToRelease = (long)stage.AmountToRelease * 1_0000_0000;
                return new Stage
                {
                    ReleaseDate = stage.ReleaseDate,
                    Amount = stageAmountToRelease,
                    Index = index + 1,
                    RatioOfTotal = (double)stageAmountToRelease / data.ProjectInfo.TargetAmount,
                };
            })
            .ToList();

        return project;
    }
}