using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using MediatR;
using Zafiro.CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class ProjectAppService(
    IProjectRepository projectRepository, IMediator mediator)
    : IProjectAppService
{
    [MemoizeTimed]
    public async Task<Result<IEnumerable<ProjectDto>>> Latest()
    {
        return await projectRepository.Latest().Map(t => t.AsEnumerable()).MapEach(project => project.ToDto());
    }

    public Task<Maybe<ProjectDto>> FindById(ProjectId projectId)
    {
        return projectRepository.Get(projectId).Map(project1 => project1.ToDto()).AsMaybe();
    }

    public Task<Result<IEnumerable<ProjectDto>>> GetFounderProjects(Guid walletId)
    {
        return mediator.Send(new GetFounderProjects.GetFounderProjectsRequest(walletId));
    }
}