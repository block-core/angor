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
            Picture = project.Picture,
            Name = project.Name,
            ShortDescription = project.ShortDescription,
            StartingDate = project.StartingDate,
            TargetAmount = project.TargetAmount,
            Stages = project.Stages.Select(stage => new StageDto
            {
                Amount = stage.Amount,
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
                AmountToRelease = (decimal)stage.Amount / 1_0000_0000,
            }).ToList(),
            FounderKey = project.FounderKey,
            FounderRecoveryKey = project.FounderRecoveryKey,
            PenaltyDays = project.PenaltyDuration.Days,
            ProjectIdentifier = project.Id.Value,
            StartDate = project.StartingDate,
            ExpiryDate = project.ExpiryDate,
        };
    }
}