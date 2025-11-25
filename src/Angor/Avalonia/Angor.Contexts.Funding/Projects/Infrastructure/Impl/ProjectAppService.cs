using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Projects.Operations;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class ProjectAppService(
    IMediator mediator)
    : IProjectAppService
{
    public Task<Result<IEnumerable<ProjectDto>>> Latest() =>
        mediator.Send(new ProjectQueries.LatestProjectsRequest());

    public Task<Result<Maybe<ProjectDto>>> TryGet(ProjectId projectId) =>
        mediator.Send(new ProjectQueries.TryGetProjectRequest(projectId));

    public Task<Result<ProjectDto>> Get(ProjectId projectId) =>
        mediator.Send(new ProjectQueries.GetProjectRequest(projectId));

    public Task<Result<IEnumerable<ProjectDto>>> GetFounderProjects(WalletId walletId)
    {
        return mediator.Send(new GetFounderProjects.GetFounderProjectsRequest(walletId));
    }
    
    public Task<Result<TransactionDraft>> CreateProject(WalletId walletId, long selectedFee, CreateProjectDto project, ProjectSeedDto seedDto)
    {
        return mediator.Send(new CreateProjectConstants.CreateProject.CreateProjectRequest(walletId, selectedFee, project, seedDto)); // WalletId and SelectedFeeRate are placeholders
    }

    public Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId)
    {
        return mediator.Send(new ProjectStatistics.ProjectStatsRequest(projectId));
    }
}
