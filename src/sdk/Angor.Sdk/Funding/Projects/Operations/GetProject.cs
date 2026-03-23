using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Projects.Operations;

public static class GetProject
{
    public record GetProjectRequest(ProjectId ProjectId) : IRequest<Result<GetProjectResponse>>;

    public record GetProjectResponse(ProjectDto Project);

    public class GetProjectHandler(IProjectService projectService)
        : IRequestHandler<GetProjectRequest, Result<GetProjectResponse>>
    {
        public async Task<Result<GetProjectResponse>> Handle(GetProjectRequest request, CancellationToken cancellationToken)
        {
            var project = await projectService.GetAsync(request.ProjectId);
            return project.Map(p => new GetProjectResponse(p.ToDto()));
        }
    }
}
