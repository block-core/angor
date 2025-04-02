using Angor.Projects.Application.Dtos;
using Angor.Projects.Domain;
using Angor.Shared.Models;
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Projects.Infrastructure.Impl;

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
                Weight = stage.Weight,
                ReleaseDate = stage.ReleaseDate,
            }).ToList()
        };
    }
    
    public static ProjectInfo ToSharedModel(this Project project)
    {
        return new ProjectInfo
        {
            TargetAmount = project.TargetAmount,
            Stages = project.Stages.Select(stage => new Stage
            {
                ReleaseDate = stage.ReleaseDate,
                AmountToRelease = stage.Amount,
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