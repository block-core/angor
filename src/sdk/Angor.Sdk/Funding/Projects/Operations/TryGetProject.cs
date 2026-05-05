using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Primitives;
using MediatR;

namespace Angor.Sdk.Funding.Projects.Operations;

public static class TryGetProject
{
    public record TryGetProjectRequest(ProjectId ProjectId) : IRequest<Result<TryGetProjectResponse>>;

    public record TryGetProjectResponse(ProjectDto? Project);

    public class TryGetProjectHandler(IProjectService projectService)
        : IRequestHandler<TryGetProjectRequest, Result<TryGetProjectResponse>>
    {
        public async Task<Result<TryGetProjectResponse>> Handle(TryGetProjectRequest request, CancellationToken cancellationToken)
        {
            var project = await projectService.TryGetAsync(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<TryGetProjectResponse>(project.Error);

            return Result.Success(new TryGetProjectResponse(project.Value?.ToDto()));
        }
    }
}
