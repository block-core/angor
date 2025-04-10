using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class ProjectAppService(
    IProjectRepository projectRepository)
    : IProjectAppService
{
    [MemoizeTimed]
    public async Task<IList<ProjectDto>> Latest()
    {
        var projects = await projectRepository.Latest();
        var projectDtos = projects.Select(project => project.ToDto());
        return projectDtos.ToList();
    }

    public Task<Maybe<ProjectDto>> FindById(ProjectId projectId)
    {
        return projectRepository.Get(projectId).Map(project1 => project1.ToDto()).AsMaybe();
    }
}