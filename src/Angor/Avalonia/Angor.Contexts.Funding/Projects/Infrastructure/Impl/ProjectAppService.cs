using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Projects.Operations;
using CSharpFunctionalExtensions;
using MediatR;
using Zafiro.CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class ProjectAppService(
    IProjectRepository projectRepository, IMediator mediator)
    : IProjectAppService
{
    public async Task<Result<IEnumerable<ProjectDto>>> Latest()
    {
        return await projectRepository.Latest().Map(t => t.AsEnumerable()).MapEach(project => project.ToDto());
    }

    public Task<Maybe<ProjectDto>> FindById(ProjectId projectId)
    {
        return projectRepository.Get(projectId).Map(p => p.ToDto()).AsMaybe();
    }

    public Task<Result<ProjectDto>> Get(ProjectId projectId)
    {
        return projectRepository.Get(projectId).Map(project => project.ToDto());
    }

    public Task<Result<IEnumerable<ProjectDto>>> GetFounderProjects(Guid walletId)
    {
        return mediator.Send(new GetFounderProjects.GetFounderProjectsRequest(walletId));
    }
    
    public Task<Result<string>> CreateProject(Guid walletId, long selectedFee, CreateProjectDto project)
    {
        return mediator.Send(new CreateProjectConstants.CreateProject.CreateProjectRequest(walletId, selectedFee, project)); // WalletId and SelectedFeeRate are placeholders
    }

    public Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId)
    {
        return mediator.Send(new ProjectStatistics.ProjectStatsRequest(projectId));
    }
}