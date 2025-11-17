using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Services;

public interface IProjectService
{
    Task<Result<Project>> GetAsync(ProjectId id);
    Task<Result<Maybe<Project>>> TryGetAsync(ProjectId projectId);
    Task<Result<IEnumerable<Project>>> GetAllAsync(params ProjectId[] ids);
    Task<Result<IEnumerable<Project>>> LatestAsync();
}