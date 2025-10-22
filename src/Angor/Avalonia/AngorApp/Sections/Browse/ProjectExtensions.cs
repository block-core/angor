using System.Linq;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.UI.Model.Implementation.Projects;

namespace AngorApp.Sections.Browse;

public static class ProjectExtensions
{
    public static IProject ToProject(this ProjectDto dto)
    {
        return new Project
        {
            Picture = dto.Avatar,
            Id = dto.Id.Value,
            InformationUri = dto.InformationUri,
            Name = dto.Name,
            NostrNpubKeyHex = dto.NostrNpubKeyHex,
            Banner = dto.Banner,
            ShortDescription = dto.ShortDescription?.Trim(),
            Stages = dto.Stages
                .Select(stage => new Stage
                {
                    Amount = stage.Amount,
                    Index = stage.Index,
                    ReleaseDate = stage.ReleaseDate,
                    RatioOfTotal = stage.RatioOfTotal
                })
                .Cast<IStage>()
                .ToList(),
            PenaltyDuration = dto.PenaltyDuration,
            PenaltyThreshold = dto.PenaltyThreshold.HasValue ?  new AmountUI(dto.PenaltyThreshold.Value) : null,
            TargetAmount = new AmountUI(dto.TargetAmount),
            StartDate = dto.FundingStartDate
        };
    }
}