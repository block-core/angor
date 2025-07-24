using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Data.Entities;
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
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations, 
        INetworkConfiguration networkConfiguration,
        IProjectEventService projectEventService,
        IUserEventService userEventService,
        IIndexerService indexerService,
        IProjectKeyService projectKeyService) : IRequestHandler<GetFounderProjectsRequest, Result<IEnumerable<ProjectDto>>>
    {
        public Task<Result<IEnumerable<ProjectDto>>> Handle(GetFounderProjectsRequest request, CancellationToken cancellationToken)
        {
             return GetProjectIds(request)
                .Bind(ids => Result.Try(() =>  ids
                    .Select(id => indexerService.GetProjectByIdAsync(id.Item1.Value)
                        .ToObservable()
                        .Where(result => result != null)
                        .Select(result => (npub:id.Item2, result!.NostrEventId,ProjectId:id.Item1.Value)))
                    .Merge()
                    .ToList()
                    .ToTask(cancellationToken)))
                .Bind(async projects =>
                {
                    await userEventService.PullAndSaveUserEventsAsync(projects.Select(p => p.npub).ToArray());
                    return Result.Success(projects.Select(p => (p.NostrEventId,p.ProjectId)).ToList());
                })
                .Bind(async ids =>
                {
                    await projectEventService.PullAndSaveProjectEventsAsync(
                        ids.Select(id => id.NostrEventId).ToArray());
                    return Result.Success(ids.Select(id => id.ProjectId).ToArray());
                })
                .Bind(ids => Result.Try(() => projectEventService.GetProjectsByIdsAsync(ids)))
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

        private async Task<Result<IEnumerable<(ProjectId,string)>>> GetProjectIds(GetFounderProjectsRequest request)
        {
            // First check if we have cached keys
            var cachedKeysResult = await projectKeyService.HasProjectsKeys(request.WalletId);
            if (cachedKeysResult is { IsSuccess: true, Value: > 0 })
            {
                return await projectKeyService.GetCachedProjectKeys(request.WalletId)
                    .Map(keys => keys.Select(k => (new ProjectId(k.ProjectId), k.NostrPubKey)));
            }
    
            // If no cached keys, derive them and cache the result
            return await seedwordsProvider.GetSensitiveData(request.WalletId)
                .Map(p => p.ToWalletWords())
                .Map(words => derivationOperations.DeriveProjectKeys(words, networkConfiguration.GetAngorKey()))
                .Map(collection => collection.Keys.AsEnumerable())
                .MapEach(keys => new ProjectKey
                {
                    FounderKey = keys.FounderKey,
                    FounderRecoveryKey = keys.FounderRecoveryKey,
                    ProjectId = keys.ProjectIdentifier,
                    NostrPubKey = keys.NostrPubKey,
                    WalletId = request.WalletId,
                    Index = keys.Index,
                    CreatedAt = DateTime.UtcNow
                })
                .Bind(async projectKeys =>
                {
                    var keys = projectKeys.ToList();
                    // Cache the derived keys
                    var saveResult = await projectKeyService.SaveProjectKeys(request.WalletId, keys);
                    return saveResult.IsSuccess 
                        ? Result.Success(keys.Select(k => (new ProjectId(k.ProjectId),k.NostrPubKey)))
                        : Result.Failure<IEnumerable<(ProjectId, string)>>(saveResult.Error);
                });
        }
    }

    public record GetFounderProjectsRequest(Guid WalletId) : IRequest<Result<IEnumerable<ProjectDto>>>;
}