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
        INetworkConfiguration networkConfiguration) : IRequestHandler<GetFounderProjectsRequest, Result<IEnumerable<ProjectDto>>>
    {
        public Task<Result<IEnumerable<ProjectDto>>> Handle(GetFounderProjectsRequest request, CancellationToken cancellationToken)
        {
            return GetProjectIds(request)
                .TraverseSequentially(projectRepository.TryGet)
                .Map(projectMaybes => projectMaybes.Values())
                .MapEach(project => project.ToDto())
                .WithTimeout(TimeSpan.FromSeconds(10));
        }

        private Task<Result<IEnumerable<ProjectId>>> GetProjectIds(GetFounderProjectsRequest request)
        {
            return seedwordsProvider.GetSensitiveData(request.WalletId)
                .Map(p => p.ToWalletWords())
                .Map(words => derivationOperations.DeriveProjectKeys(words, networkConfiguration.GetAngorKey()))
                .Map(collection => collection.Keys.AsEnumerable())
                .MapEach(keys => keys.ProjectIdentifier)
                .MapEach(fk => new ProjectId(fk));
        }
    }

    public record GetFounderProjectsRequest(Guid WalletId) : IRequest<Result<IEnumerable<ProjectDto>>>;
}