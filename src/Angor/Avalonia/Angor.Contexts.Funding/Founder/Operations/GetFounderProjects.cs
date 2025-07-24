using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Data.Services;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Zafiro.CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class GetFounderProjects
{
    public class GetFounderProjectsHandler(
        IProjectRepository projectRepository,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations, 
        INetworkConfiguration networkConfiguration,
        IProjectEventService projectEventService,
        IUserEventService userEventService,
        IIndexerService indexerService) : IRequestHandler<GetFounderProjectsRequest, Result<IEnumerable<ProjectDto>>>
    {
        public Task<Result<IEnumerable<ProjectDto>>> Handle(GetFounderProjectsRequest request, CancellationToken cancellationToken)
        {
             return GetProjectIds(request)
                .Bind(ids => Result.Try(() =>  ids
                    .Select(id => indexerService.GetProjectByIdAsync(id.Item1.Value)
                        .ToObservable()
                        .Where(result => result != null)
                        .Select(result => (npub:id.Item2, result!.NostrEventId)))
                    .Merge()
                    .ToList()
                    .ToTask(cancellationToken)))
                .Bind(async projects =>
                {
                    await userEventService.PullAndSaveUserEventsAsync(projects.Select(p => p.npub).ToArray());
                    return Result.Success(projects.Select(p => p.NostrEventId));
                })
                .Bind(ids => Result.Try(() => 
                    projectEventService.PullAndSaveProjectEventsAsync(ids.ToArray())))
                .Map(projects => projects.Select(project =>  new ProjectDto()
                {
                    Id = new ProjectId(project.ProjectId),
                    Picture = Uri.TryCreate(project.NostrUser.Picture, UriKind.Absolute, out var pictureUri) ? pictureUri : null,
                    Banner = Uri.TryCreate(project.NostrUser.Banner, UriKind.Absolute, out var bannerUri) ? bannerUri : null,
                    InformationUri = Uri.TryCreate(project.NostrUser.Website, UriKind.Absolute, out var infoUri) ? infoUri : null,
                    Name = project.NostrUser.DisplayName,
                    NostrNpubKey = project.NostrPubKey,
                    PenaltyDuration = new TimeSpan(project.PenaltyDays),
                    ShortDescription = project.NostrUser.About,
                    StartingDate = project.FundingStartDate,
                    TargetAmount = project.TargetAmount,
                    Stages = project.Stages.Select(s => new StageDto
                    {
                        ReleaseDate = s.ReleaseDate,
                        RatioOfTotal = s.AmountToRelease,
                        Index = s.StageIndex
                    }).ToList()
                }));
        }

        private Task<Result<IEnumerable<(ProjectId,string)>>> GetProjectIds(GetFounderProjectsRequest request)
        {
            return seedwordsProvider.GetSensitiveData(request.WalletId)
                .Map(p => p.ToWalletWords())
                .Map(words => derivationOperations.DeriveProjectKeys(words, networkConfiguration.GetAngorKey()))//TODO we need to change this, the derivation code requires very heavy computations
                .Map(collection => collection.Keys.AsEnumerable())
                .MapEach(keys => (keys.ProjectIdentifier,keys.NostrPubKey))
                .MapEach(fk => (new ProjectId(fk.ProjectIdentifier), fk.NostrPubKey));
        }
    }

    public record GetFounderProjectsRequest(Guid WalletId) : IRequest<Result<IEnumerable<ProjectDto>>>;
}