using System.Linq;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.UI.Model.Implementation.Projects;
using Angor.UI.Model; // for NostrKeyCodec

namespace AngorApp.Sections.Browse;

public static class ProjectExtensions
{
    public static IProject ToProject(this ProjectDto dto)
    {
        var npub = dto.NostrNpubKeyHex;
        var hex = NostrKeyCodec.TryNpubToHex(npub, out var h) ? h : string.Empty;
        return new Project
        {
            Picture = dto.Avatar,
            Id = dto.Id.Value,
            InformationUri = dto.InformationUri,
            Name = dto.Name,
            NostrNpubKey = dto.NostrNpubKeyHex,
            NpubKey = npub,
            NpubKeyHex = hex,
            Banner = dto.Banner,
            ShortDescription = dto.ShortDescription?.Trim(),
            Stages = dto.Stages
                .Select(stage => new Stage
                {
                    Amount = stage.Amount,
                    Index = stage.Index,
                    ReleaseDate = stage.ReleaseDate,
                    RatioOfTotal = (double)stage.RatioOfTotal / 100.0 // Convert to percentage the UI expects
                })
                .Cast<IStage>()
                .ToList(),
            PenaltyDuration = dto.PenaltyDuration,
            TargetAmount = new AmountUI(dto.TargetAmount),
            StartDate = dto.FundingStartDate
        };
    }
}