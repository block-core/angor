using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectInfo;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectProfile;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectConstants.CreateProject;
using Angor.Sdk.Funding.Projects.Dtos;

namespace Angor.Sdk.Funding.Projects;

public class ProjectAppService(
    IMediator mediator)
    : IProjectAppService
{
    public Task<Result<LatestProjects.LatestProjectsResponse>> Latest(LatestProjects.LatestProjectsRequest request) =>
        mediator.Send(request);

    public Task<Result<TryGetProject.TryGetProjectResponse>> TryGet(TryGetProject.TryGetProjectRequest request) =>
     mediator.Send(request);

    public Task<Result<GetProject.GetProjectResponse>> Get(GetProject.GetProjectRequest request) =>
      mediator.Send(request);

    public Task<Result<GetFounderProjects.GetFounderProjectsResponse>> GetFounderProjects(WalletId walletId)
    {
        return mediator.Send(new GetFounderProjects.GetFounderProjectsRequest(walletId));
    }

    public Task<Result<CreateProjectProfileResponse>> CreateProjectProfile(WalletId walletId, ProjectSeedDto projectSeedDto, CreateProjectDto project)
    {
        return mediator.Send(new CreateProjectProfileRequest(walletId, projectSeedDto, project));
    }

    public Task<Result<CreateProjectInfoResponse>> CreateProjectInfo(WalletId walletId, CreateProjectDto project, ProjectSeedDto projectSeedDto)
    {
        return mediator.Send(new CreateProjectInfoRequest(walletId, project, projectSeedDto));
    }

    public Task<Result<CreateProjectResponse>> CreateProject(WalletId walletId, long selectedFee, CreateProjectDto project, string projectInfoEventId, ProjectSeedDto projectSeedDto)
    {
        return mediator.Send(new CreateProjectRequest(walletId, selectedFee, project, projectInfoEventId, projectSeedDto));
    }

    public Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId)
    {
        return mediator.Send(new ProjectStatistics.ProjectStatsRequest(projectId));
    }

    public Task<Result<GetProjectRelays.GetProjectRelaysResponse>> GetRelaysForNpubAsync(string nostrPubKey)
    {
        return mediator.Send(new GetProjectRelays.GetProjectRelaysRequest(nostrPubKey));
    }
}
