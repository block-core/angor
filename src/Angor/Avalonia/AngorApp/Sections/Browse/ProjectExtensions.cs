using System.Linq;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.UI.Model.Implementation.Projects;

namespace AngorApp.Sections.Browse.ProjectLookup;

public static class ProjectExtensions
{
    public static IProject ToProject (this ProjectDto dto)
    {
        return new Project
        {
            Banner = dto.Banner,
            Id = dto.Id.Value,
            InformationUri = dto.InformationUri,
            Name = dto.Name,
            NpubKey = dto.NostrNpubKey,
            NostrNpubKey = dto.NostrNpubKey,
            Picture = dto.Picture,
            ShortDescription = dto.ShortDescription,
            Stages = dto.Stages
                .Select(stage => new Stage
                {
                    Amount = stage.Amount,
                    Index = stage.Index,
                    ReleaseDate = stage.ReleaseDate,
                    Weight = stage.Weight
                })
                .Cast<IStage>()
                .ToList(),
            PenaltyDuration =  dto.PenaltyDuration,
            TargetAmount = dto.TargetAmount,
            StartingDate = dto.StartingDate,
        };
    }
}