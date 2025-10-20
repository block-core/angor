using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Domain;

public interface IProjectRepository
{
    Task<Result<Project>> GetAsync(ProjectId id);
    Task<Result<IEnumerable<Project>>> GetAllAsync(params ProjectId[] ids);
    Task<Result<IEnumerable<Project>>> LatestAsync();
}