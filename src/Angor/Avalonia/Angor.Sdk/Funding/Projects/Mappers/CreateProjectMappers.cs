using System.Collections.Generic;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;

namespace Angor.Sdk.Funding.Projects.Mappers;

public static class CreateProjectMappers
{
    public static Project ToDomain(this CreateProjectDto dto, ProjectSeedDto seed)
    {
        return new Project
        {
            Id = new ProjectId(seed.ProjectIdentifier),
            Name = dto.ProjectName,
            ShortDescription = dto.Description,
            Picture = Uri.TryCreate(dto.AvatarUri, UriKind.Absolute, out var avatarUri) ? avatarUri : null,
            Banner = Uri.TryCreate(dto.BannerUri, UriKind.Absolute, out var bannerUri) ? bannerUri : null,
            InformationUri = Uri.TryCreate(dto.WebsiteUri, UriKind.Absolute, out var websiteUri) ? websiteUri : null,
            TargetAmount = dto.Sats ?? 0,
            StartingDate = dto.StartDate,
            EndDate = dto.EndDate ?? DateTime.MinValue,
            ExpiryDate = dto.ExpiryDate ?? DateTime.MinValue,
            PenaltyDuration = TimeSpan.FromDays(dto.PenaltyDays),
            PenaltyThreshold = dto.PenaltyThreshold,
            FounderKey = seed.FounderKey,
            FounderRecoveryKey = seed.FounderRecoveryKey,
            NostrPubKey = seed.NostrPubKey,
            ProjectType = dto.ProjectType,
            Stages = dto.Stages.Select((s, i) => new Angor.Sdk.Funding.Projects.Domain.Stage
            {
                Index = i,
                ReleaseDate = s.startDate.ToDateTime(TimeOnly.MinValue),
                RatioOfTotal = s.PercentageOfTotal
            }).ToList(),
            DynamicStagePatterns = dto.SelectedPatterns ?? new List<DynamicStagePattern>()
        };
    }
}
