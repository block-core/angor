using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class GetFounderProjects
{
    public class GetFounderProjectsHandler(
        IProjectService projectService,
        IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection) : IRequestHandler<GetFounderProjectsRequest, Result<IEnumerable<ProjectDto>>>
    {
        public async Task<Result<IEnumerable<ProjectDto>>> Handle(GetFounderProjectsRequest request, CancellationToken cancellationToken)
        {
            var storageResult = await derivedProjectKeysCollection.FindByIdAsync(request.WalletId.Value);

            if (storageResult.IsFailure || storageResult.Value == null)
                return Result.Failure<IEnumerable<ProjectDto>>(storageResult.IsFailure ? storageResult.Error
                    : "No projects found for the given wallet.");
            
            var keys = storageResult.Value!.Keys.Select(k => new ProjectId(k.ProjectIdentifier));
            
            var projects = await projectService.GetAllAsync(keys.ToArray());
            
            if (projects.IsFailure)
                return Result.Failure<IEnumerable<ProjectDto>>(projects.Error);

            var dtoList = projects.Value
                .OrderByDescending(p => p.StartingDate)
                .Select(p => p.ToDto());
            
            return Result.Success(dtoList);
        }
    }

    public record GetFounderProjectsRequest(WalletId WalletId) : IRequest<Result<IEnumerable<ProjectDto>>>;
}