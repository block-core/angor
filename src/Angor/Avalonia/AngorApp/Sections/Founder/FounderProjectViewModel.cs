using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AngorApp.Sections.Founder.Details;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Founder;

public class FounderProjectViewModel(INavigator navigation, ProjectDto dto, Func<ProjectDto, IFounderProjectDetailsViewModel> detailsFactory) : IFounderProjectViewModel
{
    public ProjectId Id { get; } = dto.Id;
    public string Name { get; } = dto.Name;
    public string ShortDescription { get; } = dto.ShortDescription;
    public Uri? Picture { get; } = dto.Picture;
    public Uri? Banner { get; } = dto.Banner;
    public long TargetAmount { get; } = dto.TargetAmount;
    public IEnhancedCommand GoToDetails => EnhancedCommand.Create(ReactiveCommand.CreateFromTask(() => navigation.Go(() => detailsFactory(dto))));
    public DateTime StartingDate { get; } = dto.StartingDate;
    public TimeSpan PenaltyDuration { get; } = dto.PenaltyDuration;
    public string NostrNpubKey { get; } = dto.NostrNpubKey;
    public Uri? InformationUri { get; } = dto.InformationUri;
    public List<StageDto> Stages { get; } = dto.Stages;
}