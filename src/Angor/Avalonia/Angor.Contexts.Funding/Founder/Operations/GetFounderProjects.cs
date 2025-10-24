using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using CSharpFunctionalExtensions;
using MediatR;
using Zafiro.CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class GetFounderProjects
{
    public class GetFounderProjectsHandler(
        IProjectService projectService,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations, 
        INetworkConfiguration networkConfiguration,
        IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection) : IRequestHandler<GetFounderProjectsRequest, Result<IEnumerable<ProjectDto>>>
    {
        public Task<Result<IEnumerable<ProjectDto>>> Handle(GetFounderProjectsRequest request, CancellationToken cancellationToken)
        {
            return GetProjectIds(request)
                .Bind(ids => projectService.GetAllAsync(ids.ToArray()))
                .MapEach(project => project.ToDto())
                .WithTimeout(TimeSpan.FromSeconds(10));
        }

        private async Task<Result<IEnumerable<ProjectId>>> GetProjectIds(GetFounderProjectsRequest request)
        {
            // Try to get from storage first
            var storageResult = await derivedProjectKeysCollection.FindByIdAsync(request.WalletId.ToString());
            
            if (storageResult.IsSuccess && storageResult.Value != null)
            {
                // Return cached project identifiers from stored keys
                return Result.Success(storageResult.Value.Keys.Select(k => new ProjectId(k.ProjectIdentifier)));
            }

            // If not in storage, derive the keys (expensive operation)
            var keysResult = await seedwordsProvider.GetSensitiveData(request.WalletId)
                .Map(p => p.ToWalletWords())
                .Map(words => derivationOperations.DeriveProjectKeys(words, networkConfiguration.GetAngorKey()));
            
            if (keysResult.IsFailure)
                return Result.Failure<IEnumerable<ProjectId>>(keysResult.Error);

            // Store complete key sets in storage for future use
            var founderKeys = keysResult.Value.Keys.ToList();

            var derivedKeys = new DerivedProjectKeys
            {
                WalletId = request.WalletId.ToString(),
                Keys = founderKeys
            };
            
            await derivedProjectKeysCollection.UpsertAsync(x => x.WalletId, derivedKeys);

            // Return project identifiers
            return Result.Success(founderKeys.Select(k => new ProjectId(k.ProjectIdentifier)));
        }
    }

    public record GetFounderProjectsRequest(Guid WalletId) : IRequest<Result<IEnumerable<ProjectDto>>>;
}