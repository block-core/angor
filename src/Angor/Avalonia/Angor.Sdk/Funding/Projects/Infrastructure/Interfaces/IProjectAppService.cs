using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Application.Dtos;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectInfo;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectProfile;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectConstants.CreateProject;

namespace Angor.Sdk.Funding.Projects.Infrastructure.Interfaces;

public interface IProjectAppService
{
    Task<Result<IEnumerable<ProjectDto>>> Latest();
    Task<Result<Maybe<ProjectDto>>> TryGet(ProjectId projectId);
    Task<Result<ProjectDto>> Get(ProjectId projectId);
    Task<Result<GetFounderProjects.GetFounderProjectsResponse>> GetFounderProjects(WalletId walletId);
    Task<Result<CreateProjectProfileResponse>> CreateProjectProfile(WalletId walletId, ProjectSeedDto projectSeedDto, CreateProjectDto project);
    Task<Result<CreateProjectInfoResponse>> CreateProjectInfo(WalletId walletId, CreateProjectDto project, ProjectSeedDto projectSeedDto);
    Task<Result<CreateProjectResponse>> CreateProject(WalletId walletId, long selectedFee, CreateProjectDto project, string projectInfoEventId, ProjectSeedDto projectSeedDto);
    Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId);
}
