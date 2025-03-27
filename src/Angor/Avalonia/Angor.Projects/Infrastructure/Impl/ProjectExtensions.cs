using Angor.Projects.Application.Dtos;
using Angor.Projects.Domain;

namespace Angor.Projects.Infrastructure.Impl;

public static class ProjectExtensions
{
    public static ProjectDto ToDto(this Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            ShortDescription = project.ShortDescription,
            StartingDate = project.StartingDate,
            TargetAmount = (long)(project.TargetAmount *  1_0000_0000),
            Stages = project.Stages.Select(stage => new StageDto
            {
                Amount = stage.Amount,
                Index = stage.Index,
                Weight = stage.Weight,
                ReleaseDate = stage.ReleaseDate,
            }).ToList()
        };
    }
}