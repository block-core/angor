using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;
using MediatR;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;

namespace Angor.Sdk.Funding.Founder.Operations;

/// <summary>
/// Loads the founder's projects from local persistence.
/// Only returns projects already known locally (added during project creation or scanning).
/// To discover new projects from the network, use <see cref="ScanFounderProjects"/>.
/// </summary>
public static class GetFounderProjects
{
    public record GetFounderProjectsRequest(WalletId WalletId) : IRequest<Result<GetFounderProjectsResponse>>;

    public record GetFounderProjectsResponse(IEnumerable<ProjectDto> Projects);

    public class GetFounderProjectsHandler(
        IProjectService projectService,
        IFounderProjectsService founderProjectsService) : IRequestHandler<GetFounderProjectsRequest, Result<GetFounderProjectsResponse>>
    {
        public async Task<Result<GetFounderProjectsResponse>> Handle(GetFounderProjectsRequest request, CancellationToken cancellationToken)
        {
            var recordsResult = await founderProjectsService.GetByWalletId(request.WalletId.Value);

            if (recordsResult.IsFailure)
                return Result.Failure<GetFounderProjectsResponse>(recordsResult.Error);

            var records = recordsResult.Value;

            if (records.Count == 0)
                return Result.Success(new GetFounderProjectsResponse(Enumerable.Empty<ProjectDto>()));

            var keys = records.Select(r => new ProjectId(r.ProjectIdentifier)).ToArray();

            var projects = await projectService.GetAllAsync(keys);

            if (projects.IsFailure)
                return Result.Failure<GetFounderProjectsResponse>(projects.Error);

            var dtoList = projects.Value
                .OrderByDescending(p => p.StartingDate)
                .Select(p => p.ToDto());

            return Result.Success(new GetFounderProjectsResponse(dtoList));
        }
    }
}
