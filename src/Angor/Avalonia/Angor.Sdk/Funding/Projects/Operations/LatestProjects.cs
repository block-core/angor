using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Projects.Operations;

public static class LatestProjects
{
    public record LatestProjectsRequest : IRequest<Result<LatestProjectsResponse>>;

    public record LatestProjectsResponse(IEnumerable<ProjectDto> Projects);

    public class LatestProjectsHandler(IProjectService projectService)
        : IRequestHandler<LatestProjectsRequest, Result<LatestProjectsResponse>>
    {
        public async Task<Result<LatestProjectsResponse>> Handle(LatestProjectsRequest request, CancellationToken cancellationToken)
        {
            // var projects = await projectService.LatestAsync();
            var projects = await projectService.LatestFromNostrAsync();
            return projects.Map(sequence => new LatestProjectsResponse(sequence.OrderByDescending(p => p.StartingDate).Select(project => project.ToDto())));
        }
    }
}
