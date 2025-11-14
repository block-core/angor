using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;

public interface IProjectAppService
{
    Task<Result<IEnumerable<ProjectDto>>> Latest();
    Task<Maybe<ProjectDto>> FindById(ProjectId projectId);
    Task<Result<ProjectDto>> Get(ProjectId projectId);
    Task<Result<IEnumerable<ProjectDto>>> GetFounderProjects(WalletId walletId);
    Task<Result<TransactionDraft>> CreateProject(WalletId walletId, long selectedFee, CreateProjectDto project);
    Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId);
}
