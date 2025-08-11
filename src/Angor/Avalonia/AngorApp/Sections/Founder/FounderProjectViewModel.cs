using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.Sections.Founder.ProjectDetails;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Founder;

public class FounderProjectViewModel(INavigator navigation, ProjectDto dto, Func<ProjectDto, IFounderProjectDetailsViewModel> detailsFactory) : IFounderProjectViewModel, IDisposable
{
    public ProjectId Id { get; } = dto.Id;
    public string Name { get; } = dto.Name;
    public string ShortDescription { get; } = dto.ShortDescription;
    public Uri? Picture { get; } = dto.Picture;
    public Uri? Banner { get; } = dto.Banner;
    public long TargetAmount { get; } = dto.TargetAmount;
    public IEnhancedCommand GoToDetails { get; } = ReactiveCommand.CreateFromTask(() => navigation.Go(() => detailsFactory(dto))).Enhance();

    public DateTime StartingDate { get; } = dto.StartingDate;
    public TimeSpan PenaltyDuration { get; } = dto.PenaltyDuration;
    public string NostrNpubKey { get; } = dto.NostrNpubKey;
    public Uri? InformationUri { get; } = dto.InformationUri;
    public List<StageDto> Stages { get; } = dto.Stages;

    public void Dispose()
    {
        GoToDetails.Dispose();
    }
}