using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;
using AngorApp.UI.Sections.Founder.ProjectDetails;
using Zafiro.UI.Navigation;
using Angor.Sdk.Funding.Projects.Dtos;

namespace AngorApp.UI.Sections.Founder;

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
