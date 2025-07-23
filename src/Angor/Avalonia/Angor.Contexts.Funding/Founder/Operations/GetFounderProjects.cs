using Angor.Contests.CrossCutting;
using Angor.Contexts.Data.Services;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
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
        IUserEventService userEventService) : IRequestHandler<GetFounderProjectsRequest, Result<IEnumerable<ProjectDto>>>
    {
        public Task<Result<IEnumerable<ProjectDto>>> Handle(GetFounderProjectsRequest request, CancellationToken cancellationToken)
        {
            return GetProjectIds(request)
                //.Bind(ids => projectRepository.GetAll(ids.ToArray()))
                .Bind(ids => Result.Try(() => projectEventService.GetProjectsByIdsAsync(ids.Select(id => id.Value).ToArray())))
                .Bind(async projects =>
                {
                    await userEventService.PullAndSaveUserEventsAsync(projects.Select(p => p.NostrPubKey).ToArray());
                    return Result.Success(projects.Select(p => p.ProjectId));
                })
                .Bind(ids => Result.Try(() =>projectEventService.GetProjectsByIdsAsync(ids.ToArray())))
                .MapEach(project => new ProjectDto()
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
                })
                .WithTimeout(TimeSpan.FromSeconds(10));
        }

        private Task<Result<IEnumerable<ProjectId>>> GetProjectIds(GetFounderProjectsRequest request)
        {
            return seedwordsProvider.GetSensitiveData(request.WalletId)
                .Map(p => p.ToWalletWords())
                .Map(words => derivationOperations.DeriveProjectKeys(words, networkConfiguration.GetAngorKey()))//TODO we need to change this, the derivation code requires very heavy computations
                .Map(collection => collection.Keys.AsEnumerable())
                .MapEach(keys => keys.ProjectIdentifier)
                .MapEach(fk => new ProjectId(fk));
        }
    }

    public record GetFounderProjectsRequest(Guid WalletId) : IRequest<Result<IEnumerable<ProjectDto>>>;
}