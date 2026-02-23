using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectInfo;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectProfile;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectConstants.CreateProject;
using Angor.Sdk.Funding.Projects.Dtos;

namespace Angor.Sdk.Funding.Projects;

public interface IProjectAppService
{
    Task<Result<LatestProjects.LatestProjectsResponse>> Latest(LatestProjects.LatestProjectsRequest request);
    Task<Result<TryGetProject.TryGetProjectResponse>> TryGet(TryGetProject.TryGetProjectRequest request);
    Task<Result<GetProject.GetProjectResponse>> Get(GetProject.GetProjectRequest request);
    Task<Result<GetFounderProjects.GetFounderProjectsResponse>> GetFounderProjects(WalletId walletId);
    Task<Result<CreateProjectProfileResponse>> CreateProjectProfile(WalletId walletId, ProjectSeedDto projectSeedDto, CreateProjectDto project);
    Task<Result<CreateProjectInfoResponse>> CreateProjectInfo(WalletId walletId, CreateProjectDto project, ProjectSeedDto projectSeedDto);
    Task<Result<CreateProjectResponse>> CreateProject(WalletId walletId, long selectedFee, CreateProjectDto project, string projectInfoEventId, ProjectSeedDto projectSeedDto);
    Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId);
    Task<Result<GetProjectRelays.GetProjectRelaysResponse>> GetRelaysForNpubAsync(string nostrPubKey);
}
