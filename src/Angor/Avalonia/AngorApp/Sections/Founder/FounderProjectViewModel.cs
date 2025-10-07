using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using AngorApp.Sections.Founder.ProjectDetails;
using AngorApp.UI.Services;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Founder;

public class FounderProjectViewModel(INavigator navigation, ProjectDto dto, IFounderAppService founderAppService, IProjectAppService projectAppService, UIServices uiServices) : IFounderProjectViewModel, IDisposable
{
    public ProjectId Id { get; } = dto.Id;
    public string Name { get; } = dto.Name;
    public string ShortDescription { get; } = dto.ShortDescription;
    public Uri? Picture { get; } = dto.Avatar;
    public Uri? Banner { get; } = dto.Banner;
    public long TargetAmount { get; } = dto.TargetAmount;
    public IEnhancedCommand GoToDetails => ReactiveCommand.CreateFromTask(() =>
    {
        return navigation.Go(() => new FounderProjectDetailsViewModel(dto.Id, projectAppService, founderAppService, uiServices));
    }).Enhance();


    public DateTime StartingDate { get; } = dto.FundingStartDate;
    public TimeSpan PenaltyDuration { get; } = dto.PenaltyDuration;
    public string NostrNpubKey { get; } = dto.NostrNpubKeyHex;
    public Uri? InformationUri { get; } = dto.InformationUri;
    public List<StageDto> Stages { get; } = dto.Stages;

    public void Dispose()
    {
        GoToDetails.Dispose();
    }
}