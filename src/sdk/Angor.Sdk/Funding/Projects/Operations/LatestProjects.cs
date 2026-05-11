using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Services;
using Angor.Shared;
using Angor.Primitives;
using MediatR;

namespace Angor.Sdk.Funding.Projects.Operations;

public static class LatestProjects
{
    public record LatestProjectsRequest : IRequest<Result<LatestProjectsResponse>>;

    public record LatestProjectsResponse(IEnumerable<ProjectDto> Projects);

    public class LatestProjectsHandler(IProjectService projectService, INetworkConfiguration networkConfiguration)
        : IRequestHandler<LatestProjectsRequest, Result<LatestProjectsResponse>>
    {
        public async Task<Result<LatestProjectsResponse>> Handle(LatestProjectsRequest request, CancellationToken cancellationToken)
        {
            var currentNetworkName = networkConfiguration.GetNetwork().Name;

            var projects = await projectService.LatestFromNostrAsync();
            if (projects.IsFailure)
                return Result.Failure<LatestProjectsResponse>(projects.Error);

            return Result.Success(new LatestProjectsResponse(
                projects.Value
                    .Where(p => IsMatchingNetwork(p.NetworkName, currentNetworkName))
                    .OrderByDescending(p => p.StartingDate)
                    .Select(project => project.ToDto())));
        }

        private static bool IsMatchingNetwork(string projectNetworkName, string currentNetworkName)
        {
            // Projects with empty/null NetworkName are legacy testnet projects (created before the field existed)
            if (string.IsNullOrEmpty(projectNetworkName))
            {
                return currentNetworkName != "Main" && currentNetworkName != "Liquid";
            }

            return string.Equals(projectNetworkName, currentNetworkName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
