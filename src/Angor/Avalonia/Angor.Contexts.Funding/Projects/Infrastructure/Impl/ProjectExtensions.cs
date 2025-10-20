using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared.Models;
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public static class ProjectExtensions
{
    public static ProjectDto ToDto(this Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Banner = project.Banner,
            Avatar = project.Picture,
            Name = project.Name,
            ShortDescription = project.ShortDescription,
            FundingStartDate = project.StartingDate,
            PenaltyDuration = project.PenaltyDuration,
            NostrNpubKeyHex = project.NostrPubKey,
            FundingEndDate = project.EndDate,
            InformationUri = project.InformationUri,
            TargetAmount = project.TargetAmount,
            PenaltyThreshold = project.PenaltyThreshold,
            Stages = project.Stages.Select(stage => new StageDto
            {
                Index = stage.Index,
                RatioOfTotal = stage.RatioOfTotal,
                ReleaseDate = stage.ReleaseDate,
            }).ToList()
        };
    }
    
    public static ProjectInfo ToProjectInfo(this Project project)
    {
        return new ProjectInfo
        {
            TargetAmount = project.TargetAmount,
            Stages = project.Stages.Select(stage => new Stage
            {
                ReleaseDate = stage.ReleaseDate,
                AmountToRelease = stage.RatioOfTotal,
            }).ToList(),
            FounderKey = project.FounderKey,
            FounderRecoveryKey = project.FounderRecoveryKey,
            PenaltyDays = project.PenaltyDuration.Days,
            PenaltyThreshold = project.PenaltyThreshold,
            ProjectIdentifier = project.Id.Value,
            StartDate = project.StartingDate,
            ExpiryDate = project.ExpiryDate,
        };
    }
}