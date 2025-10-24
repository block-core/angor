using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
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
        IStore store) : IRequestHandler<GetFounderProjectsRequest, Result<IEnumerable<ProjectDto>>>
    {
        public Task<Result<IEnumerable<ProjectDto>>> Handle(GetFounderProjectsRequest request, CancellationToken cancellationToken)
        {
            return GetProjectIds(request)
                .Bind(ids => projectRepository.GetAllAsync(ids.ToArray()))
                .MapEach(project => project.ToDto())
                .WithTimeout(TimeSpan.FromSeconds(10));
        }

        private async Task<Result<IEnumerable<ProjectId>>> GetProjectIds(GetFounderProjectsRequest request)
        {
            var result = await seedwordsProvider.GetSensitiveData(request.WalletId)
                .Map(p => p.ToWalletWords())
                .Map(words => derivationOperations.DeriveProjectKeys(words, networkConfiguration.GetAngorKey()))//TODO we need to change this, the derivation code requires very heavy computations
                .Map(collection => collection.Keys.AsEnumerable())
                .MapEach(keys => keys.ProjectIdentifier)
                .MapEach(fk => new ProjectId(fk));
            
            return result;
        }
    }

    public record GetFounderProjectsRequest(Guid WalletId) : IRequest<Result<IEnumerable<ProjectDto>>>;
}