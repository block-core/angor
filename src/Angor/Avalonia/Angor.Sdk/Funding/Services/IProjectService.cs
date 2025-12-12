using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Services;

public interface IProjectService
{
    Task<Result<Project>> GetAsync(ProjectId id);
    Task<Result<Maybe<Project>>> TryGetAsync(ProjectId projectId);
    Task<Result<IEnumerable<Project>>> GetAllAsync(params ProjectId[] ids);
    Task<Result<IEnumerable<Project>>> LatestAsync();
}