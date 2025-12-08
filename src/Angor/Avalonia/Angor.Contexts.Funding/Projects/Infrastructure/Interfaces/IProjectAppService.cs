using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using static Angor.Contexts.Funding.Founder.Operations.CreateProjectInfo;
using static Angor.Contexts.Funding.Founder.Operations.CreateProjectProfile;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;

public interface IProjectAppService
{
    Task<Result<IEnumerable<ProjectDto>>> Latest();
    Task<Result<Maybe<ProjectDto>>> TryGet(ProjectId projectId);
    Task<Result<ProjectDto>> Get(ProjectId projectId);
    Task<Result<IEnumerable<ProjectDto>>> GetFounderProjects(WalletId walletId);
    Task<Result<CreateProjectProfileResponse>> CreateProjectProfile(WalletId walletId, ProjectSeedDto projectSeedDto, CreateProjectDto project);
    Task<Result<CreateProjectInfoResponse>> CreateProjectInfo(WalletId walletId, CreateProjectDto project, ProjectSeedDto projectSeedDto);
    Task<Result<TransactionDraft>> CreateProject(WalletId walletId, long selectedFee, CreateProjectDto project, string projectInfoEventId, ProjectSeedDto projectSeedDto);
    Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId);
}
