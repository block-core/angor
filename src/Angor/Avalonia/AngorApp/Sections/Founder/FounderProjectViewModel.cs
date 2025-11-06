using System;
using System.Collections.Generic;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;
using AngorApp.Sections.Founder.ProjectDetails;
using ReactiveUI;
using Zafiro.Reactive;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Founder;

public class FounderProjectViewModel : IFounderProjectViewModel, IDisposable
{
    public FounderProjectViewModel(ProjectDto dto, INavigator navigator, Func<ProjectId, IFounderProjectDetailsViewModel> detailsFactory)
    {
        Id = dto.Id;
        Name = dto.Name;
        ShortDescription = dto.ShortDescription;
        Picture = dto.Avatar;
        Banner = dto.Banner;
        TargetAmount = dto.TargetAmount;
        StartingDate = dto.FundingStartDate;
        PenaltyDuration = dto.PenaltyDuration;
        NostrNpubKey = dto.NostrNpubKeyHex;
        InformationUri = dto.InformationUri;
        Stages = dto.Stages;

        GoToDetails = ReactiveCommand.CreateFromTask(() =>
            navigator.Go(() => detailsFactory(dto.Id))).Enhance();

    }

    public ProjectId Id { get; }
    public string Name { get; }
    public string ShortDescription { get; }
    public Uri? Picture { get; }
    public Uri? Banner { get; }
    public long TargetAmount { get; }
    public DateTime StartingDate { get; }
    public TimeSpan PenaltyDuration { get; }
    public string NostrNpubKey { get; }
    public Uri? InformationUri { get; }
    public List<StageDto> Stages { get; }
    public IEnhancedCommand GoToDetails { get; }

    public void Dispose()
    {
        GoToDetails.Dispose();
    }
}
