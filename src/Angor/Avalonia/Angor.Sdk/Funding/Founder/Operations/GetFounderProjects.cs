using Angor.Sdk.Common;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using CSharpFunctionalExtensions;
using MediatR;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class GetFounderProjects
{
    public record GetFounderProjectsRequest(WalletId WalletId) : IRequest<Result<GetFounderProjectsResponse>>;

    public record GetFounderProjectsResponse(IEnumerable<ProjectDto> Projects);

    public class GetFounderProjectsHandler(
        IProjectService projectService,
        IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection) : IRequestHandler<GetFounderProjectsRequest, Result<GetFounderProjectsResponse>>
    {
        public async Task<Result<GetFounderProjectsResponse>> Handle(GetFounderProjectsRequest request, CancellationToken cancellationToken)
        {
            var storageResult = await derivedProjectKeysCollection.FindByIdAsync(request.WalletId.Value);

            if (storageResult.IsFailure || storageResult.Value == null)
                return Result.Failure<GetFounderProjectsResponse>(storageResult.IsFailure ? storageResult.Error
                    : "No projects found for the given wallet.");
            
            var keys = storageResult.Value!.Keys.Select(k => new ProjectId(k.ProjectIdentifier));
            
            var projects = await projectService.GetAllAsync(keys.ToArray());
            
            if (projects.IsFailure)
                return Result.Failure<GetFounderProjectsResponse>(projects.Error);

            var dtoList = projects.Value
                .OrderByDescending(p => p.StartingDate)
                .Select(p => p.ToDto());
            
            return Result.Success(new GetFounderProjectsResponse(dtoList));
        }
    }
}