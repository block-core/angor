using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Projects.Operations;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;
using static Angor.Contexts.Funding.Founder.Operations.CreateProjectInfo;
using static Angor.Contexts.Funding.Founder.Operations.CreateProjectProfile;

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
    
    public Task<Result<CreateProjectProfileResponse>> CreateProjectProfile(WalletId walletId, CreateProjectDto project)
    {
        return mediator.Send(new CreateProjectProfile.CreateProjectProfileRequest(walletId, project));
    }

    public Task<Result<CreateProjectInfoResponse>> CreateProjectInfo(WalletId walletId, CreateProjectDto project, FounderKeys FounderKeys)
    {
        return mediator.Send(new CreateProjectInfo.CreateProjectInfoRequest(walletId, project, FounderKeys));
    }
    
    public Task<Result<TransactionDraft>> CreateProject(WalletId walletId, long selectedFee, CreateProjectDto project, string projectInfoEventId, FounderKeys FounderKeys)
    {
        return mediator.Send(new CreateProjectConstants.CreateProject.CreateProjectRequest(walletId, selectedFee, project, projectInfoEventId, FounderKeys));
    }

    public Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId)
    {
        return mediator.Send(new ProjectStatistics.ProjectStatsRequest(projectId));
    }
}
