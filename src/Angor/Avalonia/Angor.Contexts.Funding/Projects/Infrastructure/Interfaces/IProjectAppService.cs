using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;

public interface IProjectAppService
{
    Task<Result<IEnumerable<ProjectDto>>> Latest();
    Task<Maybe<ProjectDto>> FindById(ProjectId projectId);
    Task<Result<IEnumerable<ProjectDto>>> GetFounderProjects(Guid walletId);
    Task<Result<string>> CreateProject(Guid walletId, long selectedFee, CreateProjectDto project);
    Task<Result<ProjectStatisticsDto>> GetProjectStatistics(ProjectId projectId);
}
