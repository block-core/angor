using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Application.Dtos;
using Angor.Sdk.Funding.Projects.Infrastructure.Interfaces;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectInfo;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectProfile;

namespace Angor.Sdk.Funding.Projects.Infrastructure.Impl;

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
    
    public Task<Result<CreateProjectProfileResponse>> CreateProjectProfile(WalletId walletId, ProjectSeedDto projectSeedDto, CreateProjectDto project)
    {
        return mediator.Send(new CreateProjectProfile.CreateProjectProfileRequest(walletId, projectSeedDto, project));
    }

    public Task<Result<CreateProjectInfoResponse>> CreateProjectInfo(WalletId walletId, CreateProjectDto project, ProjectSeedDto projectSeedDto)
    {
        return mediator.Send(new CreateProjectInfo.CreateProjectInfoRequest(walletId, project, projectSeedDto));
    }
    
    public Task<Result<TransactionDraft>> CreateProject(WalletId walletId, long selectedFee, CreateProjectDto project, string projectInfoEventId, ProjectSeedDto projectSeedDto)
    {
        return mediator.Send(new CreateProjectConstants.CreateProject.CreateProjectRequest(walletId, selectedFee, project, projectInfoEventId, projectSeedDto));
    }

    public Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId)
    {
        return mediator.Send(new ProjectStatistics.ProjectStatsRequest(projectId));
    }
}
