using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Projects.Operations;

public static class TryGetProject
{
    public record TryGetProjectRequest(ProjectId ProjectId) : IRequest<Result<TryGetProjectResponse>>;

    public record TryGetProjectResponse(Maybe<ProjectDto> Project);

    public class TryGetProjectHandler(IProjectService projectService)
        : IRequestHandler<TryGetProjectRequest, Result<TryGetProjectResponse>>
    {
        public async Task<Result<TryGetProjectResponse>> Handle(TryGetProjectRequest request, CancellationToken cancellationToken)
        {
            var project = await projectService.TryGetAsync(request.ProjectId);
            return project.Map(maybe => new TryGetProjectResponse(maybe.Map(p => p.ToDto())));
        }
    }
}
