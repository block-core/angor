using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Projects.Operations;

public static class ProjectQueries
{
    public record LatestProjectsRequest : IRequest<Result<IEnumerable<ProjectDto>>>;

    public class LatestProjectsHandler(IProjectService projectService)
        : IRequestHandler<LatestProjectsRequest, Result<IEnumerable<ProjectDto>>>
    {
        public async Task<Result<IEnumerable<ProjectDto>>> Handle(LatestProjectsRequest request, CancellationToken cancellationToken)
        {
            var projects = await projectService.LatestAsync();
            return projects.Map(sequence => sequence.Select(project => project.ToDto()));
        }
    }

    public record GetProjectRequest(ProjectId ProjectId) : IRequest<Result<ProjectDto>>;

    public class GetProjectHandler(IProjectService projectService)
        : IRequestHandler<GetProjectRequest, Result<ProjectDto>>
    {
        public async Task<Result<ProjectDto>> Handle(GetProjectRequest request, CancellationToken cancellationToken)
        {
            var project = await projectService.GetAsync(request.ProjectId);
            return project.Map(p => p.ToDto());
        }
    }

    public record TryGetProjectRequest(ProjectId ProjectId) : IRequest<Result<Maybe<ProjectDto>>>;

    public class TryGetProjectHandler(IProjectService projectService)
        : IRequestHandler<TryGetProjectRequest, Result<Maybe<ProjectDto>>>
    {
        public async Task<Result<Maybe<ProjectDto>>> Handle(TryGetProjectRequest request, CancellationToken cancellationToken)
        {
            var project = await projectService.TryGetAsync(request.ProjectId);
            return project.Map(maybe => maybe.Map(p => p.ToDto()));
        }
    }
}
